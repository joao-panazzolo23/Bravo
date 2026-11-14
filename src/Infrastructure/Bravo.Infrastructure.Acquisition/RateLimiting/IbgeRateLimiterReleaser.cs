namespace Bravo.Infrastructure.Acquisition.RateLimiting;

internal sealed class IbgeRateLimiterReleaser(SemaphoreSlim semaphore) : IDisposable
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

