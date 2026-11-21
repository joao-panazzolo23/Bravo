using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Downloads;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Tse;

public sealed class TseHttpClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tse-dl-{Guid.NewGuid():N}");

    public TseHttpClientTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);

            return Task.FromResult(responder(request, Interlocked.Increment(ref _calls)));
        }
    }

    private sealed record Harness(TseHttpClient Client, StubHandler Handler);

    private Harness CreateClient(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder,
        Action<TseOptions>? configure = null)
    {
        var options = new TseOptions
        {
            TempDirectory = _root,
            RetryBaseDelayMs = 1,
            RetryMaxDelayMs = 2,
            MaxRetryAttempts = 3
        };

        configure?.Invoke(options);

        var value = Options.Create(options);
        var handler = new StubHandler(responder);

        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(options.BaseAddress)
        };

        var client = new TseHttpClient(
            httpClient,
            new TseRateLimiter(value),
            new TseBackoffPolicy(value),
            value,
            NullLogger<TseHttpClient>.Instance);

        return new Harness(client, handler);
    }

    private static HttpResponseMessage Ok(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.Latin1)
        };

    [Fact]
    public async Task DownloadsFileAndComputesSha256()
    {
        var payload = "A;B\n1;2\n";
        var harness = CreateClient((_, _) => Ok(payload));

        var result = await harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        result.FileName.Should().Be("consulta_cand_2022.zip");
        result.SizeBytes.Should().Be(Encoding.Latin1.GetByteCount(payload));
        result.Sha256.Should().Be(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(payload))));
        File.Exists(result.FilePath).Should().BeTrue();
        File.Exists(result.FilePath + ".part").Should().BeFalse();
    }

    [Fact]
    public async Task RequestsTheCandidateArchiveForTheRequestedYear()
    {
        var harness = CreateClient((_, _) => Ok("ok"));

        await harness.Client.DownloadCandidateArchiveAsync(2018, _root);

        harness.Handler.RequestedUris.Should().ContainSingle()
            .Which.AbsoluteUri.Should().Be(
                "https://cdn.tse.jus.br/estatistica/sead/odsele/consulta_cand/consulta_cand_2018.zip");
    }

    [Fact]
    public async Task RequestsTheResultsArchiveForTheRequestedState()
    {
        var harness = CreateClient((_, _) => Ok("ok"));

        await harness.Client.DownloadSectionResultsArchiveAsync(2022, "sp", _root);

        harness.Handler.RequestedUris.Should().ContainSingle()
            .Which.AbsoluteUri.Should().Be(
                "https://cdn.tse.jus.br/estatistica/sead/odsele/votacao_secao/votacao_secao_2022_SP.zip");
    }

    [Fact]
    public async Task RetriesTransientServerErrorThenSucceeds()
    {
        var harness = CreateClient((_, call) => call < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Ok("ok"));

        var result = await harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        File.ReadAllText(result.FilePath).Should().Be("ok");
    }

    [Fact]
    public async Task RetriesTooManyRequestsAndHonorsRetryAfter()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);

        var harness = CreateClient((_, call) => call == 1 ? throttled : Ok("ok"));

        var result = await harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        File.ReadAllText(result.FilePath).Should().Be("ok");
        harness.Handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task GivesUpAfterMaxAttempts()
    {
        var harness = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var act = () => harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        await act.Should().ThrowAsync<TseDownloadException>();
        harness.Handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task DoesNotRetryClientError()
    {
        var harness = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var act = () => harness.Client.DownloadCandidateArchiveAsync(2018, _root);

        await act.Should().ThrowAsync<TseDownloadException>();
        harness.Handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RejectsDeclaredSizeAboveLimit()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("x")
        };

        response.Content.Headers.ContentLength = 10_000;

        var harness = CreateClient(
            (_, _) => response,
            options => options.MaxDownloadBytes = 1_000);

        var act = () => harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        await act.Should().ThrowAsync<TseDownloadException>().WithMessage("*exceeds*");
    }

    [Fact]
    public async Task LeavesNoPartialFileBehindOnFailure()
    {
        var harness = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var act = () => harness.Client.DownloadCandidateArchiveAsync(2022, _root);

        await act.Should().ThrowAsync<TseDownloadException>();
        Directory.GetFiles(_root, "*.part").Should().BeEmpty();
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var harness = CreateClient((_, _) => Ok("x"));

        var act = () => harness.Client.DownloadCandidateArchiveAsync(2022, _root, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
