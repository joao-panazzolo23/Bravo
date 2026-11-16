using System.Threading.RateLimiting;
using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.RateLimiting;

public sealed class TseRateLimiter : IDisposable
{
    private readonly ConcurrencyLimiter _limiter;

    public TseRateLimiter(IOptions<TseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = Math.Max(1, options.Value.MaxConcurrentDownloads),
            QueueLimit = Math.Max(1, options.Value.MaxConcurrentDownloads),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken)
        => _limiter.AcquireAsync(1, cancellationToken);

    public void Dispose() => _limiter.Dispose();
}
