using Bravo.Infrastructure.Acquisition.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.RateLimiting;

public sealed class IbgeRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly TimeSpan _minDelay;
    private readonly Lock _lock = new();
    private DateTime _lastRequestTime = DateTime.MinValue;

    public IbgeRateLimiter(IOptions<IbgeOptions> options)
    {
        var opts = options.Value;
        var maxConcurrency = Math.Max(1, opts.MaxConcurrentRequests);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _minDelay = TimeSpan.FromMilliseconds(Math.Max(0, opts.MinDelayBetweenRequestsMs));
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);

        TimeSpan delayNeeded;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRequestTime;
            if (elapsed < _minDelay)
            {
                delayNeeded = _minDelay - elapsed;
                _lastRequestTime = now + delayNeeded;
            }
            else
            {
                delayNeeded = TimeSpan.Zero;
                _lastRequestTime = now;
            }
        }

        if (delayNeeded > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(delayNeeded, cancellationToken);
            }
            catch
            {
                _concurrencySemaphore.Release();
                throw;
            }
        }

        return new IbgeRateLimiterReleaser(_concurrencySemaphore);
    }

    public void Dispose()
    {
        _concurrencySemaphore.Dispose();
    }
}
