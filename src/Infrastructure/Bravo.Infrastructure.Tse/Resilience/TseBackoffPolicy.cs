using Bravo.Infrastructure.Tse.Options;

namespace Bravo.Infrastructure.Tse.Resilience;

public sealed class TseBackoffPolicy(TseOptions options)
{
    public TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } requested && requested > TimeSpan.Zero)
            return requested;

        var exponent = Math.Max(0, attempt - 1);
        var raw = options.RetryBaseDelayMs * Math.Pow(2, exponent);
        var capped = Math.Min(options.RetryMaxDelayMs, raw);

        var jitter = 0.85 + (Random.Shared.NextDouble() * 0.3);

        return TimeSpan.FromMilliseconds(capped * jitter);
    }

    public int MaxAttempts => Math.Max(1, options.MaxRetryAttempts);
}
