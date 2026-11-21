using System.Threading.RateLimiting;
using Bravo.Infrastructure.Ibge.Options;

namespace Bravo.Infrastructure.Ibge.RateLimiting;

public sealed class IbgeRateLimiter : IDisposable
{
    private readonly TokenBucketRateLimiter _limiter;

    public IbgeRateLimiter(IbgeOptions options)
    {
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, options.MaxConcurrentRequests),
            QueueLimit = Math.Max(1, options.MaxConcurrentRequests),
            TokensPerPeriod = Math.Max(1, options.MaxConcurrentRequests),
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(Math.Max(1, options.MinDelayBetweenRequestsMs)),
            AutoReplenishment = true
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken)
        => _limiter.AcquireAsync(1, cancellationToken);

    public void Dispose() => _limiter.Dispose();
}
