using System.Diagnostics;
using Bravo.Application.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Acquisition;

public class HostRateLimiterTests
{
    private static HostRateLimiterRegistry Registry(AcquisitionOptions options) =>
        new(Options.Create(options));

    [Fact]
    public async Task AcquireAsync_ShouldEnforceMinimumDelayBetweenRequests()
    {
        var stopwatch = Stopwatch.StartNew();
        using var limiter = new HostRateLimiter(5, 50);

        using (await limiter.AcquireAsync(CancellationToken.None))
        {
        }

        using (await limiter.AcquireAsync(CancellationToken.None))
        {
        }

        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public async Task AcquireAsync_ShouldThrottleMaxConcurrency()
    {
        using var limiter = new HostRateLimiter(2, 0);
        var active = 0;
        var maxActive = 0;
        var gate = new object();

        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            using (await limiter.AcquireAsync(CancellationToken.None))
            {
                lock (gate)
                {
                    active++;
                    maxActive = Math.Max(maxActive, active);
                }

                await Task.Delay(30);

                lock (gate)
                {
                    active--;
                }
            }
        });

        await Task.WhenAll(tasks);
        maxActive.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task AcquireAsync_ShouldThrowWhenCancelled()
    {
        using var limiter = new HostRateLimiter(1, 0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            using var _ = await limiter.AcquireAsync(cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Registry_ShouldReturnIndependentLimiterPerConfiguredHost()
    {
        var options = new AcquisitionOptions
        {
            Ibge = new ProviderOptions { MaxConcurrentRequests = 1, MinDelayBetweenRequestsMs = 0, AllowedHosts = ["servicodados.ibge.gov.br"] },
            Tse = new ProviderOptions { MaxConcurrentRequests = 2, MinDelayBetweenRequestsMs = 0, AllowedHosts = ["dadosabertos.tse.jus.br", "cdn.tse.jus.br"] },
            Fgv = new ProviderOptions { MaxConcurrentRequests = 1, MinDelayBetweenRequestsMs = 0, AllowedHosts = ["raw.githubusercontent.com"] }
        };

        using var registry = Registry(options);

        var tseCdn = registry.For(new Uri("https://cdn.tse.jus.br/file.zip"));
        var ibge = registry.For(new Uri("https://servicodados.ibge.gov.br/api/v1/localidades/estados"));
        var tseCatalog = registry.For(new Uri("https://dadosabertos.tse.jus.br/api/3/action/package_show"));
        var tseCdnAgain = registry.For(new Uri("https://cdn.tse.jus.br/other.zip"));

        tseCdn.Should().NotBeSameAs(ibge);
        tseCdn.Should().NotBeSameAs(tseCatalog);
        tseCdn.Should().BeSameAs(tseCdnAgain);
    }
}
