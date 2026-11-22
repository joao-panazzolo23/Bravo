using System.Net;
using System.Text;
using Bravo.Application.Imports.Common.Exceptions;
using Bravo.Application.Imports.Exceptions;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Ibge.Options;
using Bravo.Infrastructure.Ibge.RateLimiting;
using FluentAssertions;

namespace Bravo.UnitTests.Ibge;

/// <summary>
/// Pins the transport behaviour that moved out of <c>IbgeReader</c>: the exact endpoints, the
/// streamed JSON decoding, and the classification of provider failures.
/// </summary>
public sealed class IbgeHttpClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);

            return Task.FromResult(responder(request));
        }
    }

    private sealed record Harness(IbgeHttpClient Client, StubHandler Handler);

    private static Harness CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var options = new IbgeOptions
        {
            BaseAddress = "https://servicodados.ibge.gov.br/",
            MaxConcurrentRequests = 1,
            MinDelayBetweenRequestsMs = 1
        };

        var handler = new StubHandler(responder);

        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(options.BaseAddress)
        };

        return new Harness(
            new IbgeHttpClient(httpClient, new IbgeRateLimiter(options)),
            handler);
    }

    private static HttpResponseMessage Json(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task StreamsStatesFromTheOrderedEndpoint()
    {
        const string payload = """
            [
              { "id": 11, "sigla": "RO", "nome": "Rondônia", "regiao": { "id": 1, "sigla": "N", "nome": "Norte" } },
              { "id": 12, "sigla": "AC", "nome": "Acre", "regiao": { "id": 1, "sigla": "N", "nome": "Norte" } }
            ]
            """;

        var harness = CreateClient(_ => Json(payload));

        var states = new List<string?>();

        await foreach (var state in harness.Client.StreamStatesAsync(CancellationToken.None))
            states.Add(state?.Sail);

        states.Should().Equal("RO", "AC");
        harness.Handler.RequestedUris.Should().ContainSingle()
            .Which.PathAndQuery.Should().Be("/api/v1/localidades/estados?orderBy=nome");
    }

    [Fact]
    public async Task StreamsMunicipalitiesFromTheOrderedEndpoint()
    {
        const string payload = """
            [
              { "id": 1100015, "nome": "Alta Floresta D'Oeste" },
              { "id": 1100023, "nome": "Ariquemes" }
            ]
            """;

        var harness = CreateClient(_ => Json(payload));

        var municipalities = new List<string>();

        await foreach (var municipality in harness.Client.StreamMunicipalitiesAsync(CancellationToken.None))
        {
            if (municipality is not null)
                municipalities.Add(municipality.Nome);
        }

        municipalities.Should().Equal("Alta Floresta D'Oeste", "Ariquemes");
        harness.Handler.RequestedUris.Should().ContainSingle()
            .Which.PathAndQuery.Should().Be("/api/v1/localidades/municipios?orderBy=nome");
    }

    [Fact]
    public async Task TooManyRequestsIsTransientAndCarriesRetryAfter()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(7));

        var harness = CreateClient(_ => throttled);

        var act = async () =>
        {
            await foreach (var _ in harness.Client.StreamStatesAsync(CancellationToken.None))
            {
            }
        };

        var exception = (await act.Should().ThrowAsync<ImportTransientException>()).Which;
        exception.RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task ServerErrorIsTransient()
    {
        var harness = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var act = async () =>
        {
            await foreach (var _ in harness.Client.StreamStatesAsync(CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<ImportTransientException>();
    }

    [Fact]
    public async Task ClientErrorIsPermanent()
    {
        var harness = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var act = async () =>
        {
            await foreach (var _ in harness.Client.StreamStatesAsync(CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<ImportPermanentException>();
    }

    [Fact]
    public async Task HonorsCancellationBeforeRequesting()
    {
        var harness = CreateClient(_ => Json("[]"));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () =>
        {
            await foreach (var _ in harness.Client.StreamStatesAsync(cancellation.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        harness.Handler.RequestedUris.Should().BeEmpty();
    }
}
