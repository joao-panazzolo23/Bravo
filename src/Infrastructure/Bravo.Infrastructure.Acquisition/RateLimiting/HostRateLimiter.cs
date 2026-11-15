using Bravo.Application.Acquisition.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.RateLimiting;

/// <summary>
/// Per-host limiter enforcing both a concurrency ceiling and a minimum delay between
/// request starts. The limiter is shared by every request sent to the host, including
/// retries, so a retry storm cannot bypass the configured policy.
/// </summary>
public sealed class HostRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly TimeSpan _minDelay;
    private readonly Lock _lock = new();
    private DateTime _lastRequestTime = DateTime.MinValue;

    public HostRateLimiter(int maxConcurrency, int minDelayBetweenRequestsMs)
    {
        _concurrencySemaphore = new SemaphoreSlim(
            Math.Max(1, maxConcurrency),
            Math.Max(1, maxConcurrency));
        _minDelay = TimeSpan.FromMilliseconds(Math.Max(0, minDelayBetweenRequestsMs));
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

        return new RateLimiterReleaser(_concurrencySemaphore);
    }

    public void Dispose() => _concurrencySemaphore.Dispose();
}

/// <summary>
/// Registry of independent limiters, one per external host.
/// </summary>
public sealed class HostRateLimiterRegistry : IDisposable
{
    private readonly Dictionary<string, HostRateLimiter> _limiters;
    private readonly Lock _lock = new();

    public HostRateLimiterRegistry(IOptions<AcquisitionOptions> options)
    {
        var value = options.Value;
        _limiters = new Dictionary<string, HostRateLimiter>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in new[] { value.Ibge, value.Tse, value.Fgv })
        {
            foreach (var host in provider.AllowedHosts)
            {
                _limiters.TryAdd(
                    host,
                    new HostRateLimiter(provider.MaxConcurrentRequests, provider.MinDelayBetweenRequestsMs));
            }
        }
    }

    public HostRateLimiter For(Uri uri)
    {
        var host = uri.Host;
        lock (_lock)
        {
            if (_limiters.TryGetValue(host, out var limiter))
            {
                return limiter;
            }

            // Defensive: an allowlisted host should always be registered, but a misconfigured
            // catalog must not allow unlimited traffic.
            var fallback = new HostRateLimiter(1, 1000);
            _limiters[host] = fallback;
            return fallback;
        }
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}

internal sealed class RateLimiterReleaser(SemaphoreSlim semaphore) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            semaphore.Release();
        }
    }
}
