using System.Diagnostics;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Acquisition;

public class IbgeRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_ShouldEnforceMinimumDelayBetweenRequests()
    {
        var options = Options.Create(new IbgeOptions
        {
            MaxConcurrentRequests = 5,
            MinDelayBetweenRequestsMs = 50
        });

        using var limiter = new IbgeRateLimiter(options);

        var stopwatch = Stopwatch.StartNew();

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
        var options = Options.Create(new IbgeOptions
        {
            MaxConcurrentRequests = 2,
            MinDelayBetweenRequestsMs = 0
        });

        using var limiter = new IbgeRateLimiter(options);
        var activeCount = 0;
        var maxActiveCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            using (await limiter.AcquireAsync(CancellationToken.None))
            {
                lock (lockObj)
                {
                    activeCount++;
                    if (activeCount > maxActiveCount)
                    {
                        maxActiveCount = activeCount;
                    }
                }

                await Task.Delay(30);

                lock (lockObj)
                {
                    activeCount--;
                }
            }
        });

        await Task.WhenAll(tasks);
        maxActiveCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task AcquireAsync_ShouldThrowWhenCancelled()
    {
        var options = Options.Create(new IbgeOptions
        {
            MaxConcurrentRequests = 1,
            MinDelayBetweenRequestsMs = 0
        });

        using var limiter = new IbgeRateLimiter(options);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            using var _ = await limiter.AcquireAsync(cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
