using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Resilience;

public sealed class TseBackoffPolicy(IOptions<TseOptions> options)
{
    private readonly TseOptions _options = options.Value;

    public TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } requested && requested > TimeSpan.Zero)
            return requested;

        var exponent = Math.Max(0, attempt - 1);
        var raw = _options.RetryBaseDelayMs * Math.Pow(2, exponent);
        var capped = Math.Min(_options.RetryMaxDelayMs, raw);

        var jitter = 0.85 + (Random.Shared.NextDouble() * 0.3);

        return TimeSpan.FromMilliseconds(capped * jitter);
    }

    public int MaxAttempts => Math.Max(1, _options.MaxRetryAttempts);
}
