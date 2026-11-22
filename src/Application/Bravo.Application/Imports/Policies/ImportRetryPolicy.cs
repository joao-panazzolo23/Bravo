using Bravo.Application.Imports.Common.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Imports.Common.Policies;

public sealed class ImportRetryPolicy(
    IOptions<ImportEngineOptions> options
)
{
    public TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } requestedDelay && requestedDelay > TimeSpan.Zero)
            return requestedDelay;

        var settings = options.Value;
        var exponent = Math.Max(0, attempt - 1);
        var delayMilliseconds = Math.Min(
            settings.RetryMaxDelayMilliseconds,
            settings.RetryBaseDelayMilliseconds * Math.Pow(2, exponent));

        var jitter = 0.85 + (Random.Shared.NextDouble() * 0.3);
        return TimeSpan.FromMilliseconds(delayMilliseconds * jitter);
    }
}
