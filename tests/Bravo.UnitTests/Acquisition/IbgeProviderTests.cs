using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Bravo.Infrastructure.Acquisition.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Acquisition;

public class IbgeProviderTests
{
    private const string StatesJson = """
        [
            {"id":35,"sigla":"SP","nome":"São Paulo","regiao":{"id":3,"sigla":"SE","nome":"Sudeste"}},
            {"id":33,"sigla":"RJ","nome":"Rio de Janeiro","regiao":{"id":3,"sigla":"SE","nome":"Sudeste"}}
        ]
        """;

    private const string MunicipalitiesJson = """
        [
            {"id":3550308,"nome":"São Paulo","UF":{"id":35,"sigla":"SP","nome":"São Paulo"}},
            {"id":3304557,"nome":"Rio de Janeiro","UF":{"id":33,"sigla":"RJ","nome":"Rio de Janeiro"}}
        ]
        """;

    // [Fact]
    // public async Task StreamStates_ShouldParseAndYieldStateRecords()
    // {
    //     var handler = new TestHttpMessageHandler(_ =>
    //     {
    //         var res = new HttpResponseMessage(HttpStatusCode.OK)
    //         {
    //             Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
    //         };
    //         return Task.FromResult(res);
    //     });

    //     var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.ibge.gov.br/") };

    //     var options = Options.Create(new IbgeOptions { MaxConcurrentRequests = 2, MinDelayBetweenRequestsMs = 0 });
    //     using var limiter = new IbgeRateLimiter(options);
    //     var pipeline = IbgeResiliencePipelineFactory.Create(options.Value, NullLogger<IbgeProvider>.Instance);
    //     var provider = new IbgeProvider(client, limiter, pipeline, NullLogger<IbgeProvider>.Instance);

    //     var states = new List<Application.Acquisition.Providers.IbgeStateRecord>();
    //     await foreach (var s in provider.StreamStates(CancellationToken.None))
    //     {
    //         states.Add(s);
    //     }

    //     states.Should().HaveCount(2);
    //     states[0].Code.Should().Be(35);
    //     states[0].Abbreviation.Should().Be("SP");
    //     states[0].Name.Should().Be("São Paulo");
    //     states[0].Region.Should().Be("SE");

    //     states[1].Code.Should().Be(33);
    //     states[1].Abbreviation.Should().Be("RJ");
    // }

    // [Fact]
    // public async Task StreamMunicipalities_ShouldParseAndYieldMunicipalityRecords()
    // {
    //     var handler = new TestHttpMessageHandler(_ =>
    //     {
    //         var res = new HttpResponseMessage(HttpStatusCode.OK)
    //         {
    //             Content = new StringContent(MunicipalitiesJson, Encoding.UTF8, "application/json")
    //         };
    //         return Task.FromResult(res);
    //     });

    //     var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.ibge.gov.br/") };

    //     var options = Options.Create(new IbgeOptions { MaxConcurrentRequests = 2, MinDelayBetweenRequestsMs = 0 });
    //     using var limiter = new IbgeRateLimiter(options);
    //     // var pipeline = IbgeResiliencePipelineFactory.Create(options.Value, NullLogger<IbgeProvider>.Instance);
    //     // var provider = new IbgeProvider(client, limiter, pipeline, NullLogger<IbgeProvider>.Instance);

    //     var munis = new List<Application.Acquisition.Providers.IbgeMunicipalityRecord>();
    //     await foreach (var m in provider.StreamMunicipalities(CancellationToken.None))
    //     {
    //         munis.Add(m);
    //     }

    //     munis.Should().HaveCount(2);
    //     munis[0].Code.Should().Be(3550308);
    //     munis[0].Name.Should().Be("São Paulo");
    //     munis[0].StateCode.Should().Be(35);

    //     munis[1].Code.Should().Be(3304557);
    //     munis[1].StateCode.Should().Be(33);
    // }

    [Fact]
    public async Task StreamStates_ShouldRetryOn429AndSucceed()
    {
        var attempts = 0;
        var handler = new TestHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                var response429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response429.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
                return Task.FromResult(response429);
            }

            var okResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(okResponse);
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.ibge.gov.br/") };

        var options = Options.Create(new IbgeOptions { MaxConcurrentRequests = 2, MinDelayBetweenRequestsMs = 0, MaxRetryAttempts = 3, RetryBaseDelayMs = 10 });
        using var limiter = new IbgeRateLimiter(options);
        // var pipeline = IbgeResiliencePipelineFactory.Create(options.Value, NullLogger<IbgeProvider>.Instance);
        // var provider = new IbgeProvider(client, limiter, pipeline, NullLogger<IbgeProvider>.Instance);

        var states = new List<Application.Acquisition.Providers.IbgeStateRecord>();
        // await foreach (var s in provider.StreamStates(CancellationToken.None))
        // {
        //     states.Add(s);
        // }

        attempts.Should().Be(2);
        states.Should().HaveCount(2);
    }

    //     [Fact]
    //     public async Task StreamStates_ShouldRetryOn500AndSucceed()
    //     {
    //         var attempts = 0;
    //         var handler = new TestHttpMessageHandler(_ =>
    //         {
    //             attempts++;
    //             if (attempts == 1)
    //             {
    //                 return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    //             }

    //             var okResponse = new HttpResponseMessage(HttpStatusCode.OK)
    //             {
    //                 Content = new StringContent(StatesJson, Encoding.UTF8, "application/json")
    //             };
    //             return Task.FromResult(okResponse);
    //         });

    //         var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.ibge.gov.br/") };

    //         var options = Options.Create(new IbgeOptions { MaxConcurrentRequests = 2, MinDelayBetweenRequestsMs = 0, MaxRetryAttempts = 3, RetryBaseDelayMs = 10 });
    //         using var limiter = new IbgeRateLimiter(options);
    //         var pipeline = IbgeResiliencePipelineFactory.Create(options.Value, NullLogger<IbgeProvider>.Instance);
    //         var provider = new IbgeProvider(client, limiter, pipeline, NullLogger<IbgeProvider>.Instance);

    //         var states = new List<Application.Acquisition.Providers.IbgeStateRecord>();
    //         await foreach (var s in provider.StreamStates(CancellationToken.None))
    //         {
    //             states.Add(s);
    //         }

    //         attempts.Should().Be(2);
    //         states.Should().HaveCount(2);
    //     }
}
