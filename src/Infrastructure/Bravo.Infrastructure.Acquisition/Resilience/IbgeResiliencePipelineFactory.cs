using System.Net;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.Providers;
using Microsoft.Extensions.Logging;
using Paramore.Fences;
using Paramore.Fences.Retry;
using Paramore.Fences.Timeout;

namespace Bravo.Infrastructure.Acquisition.Resilience;

public static class IbgeResiliencePipelineFactory
{
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

    public static ResiliencePipeline<HttpResponseMessage> Create(
        IbgeOptions options,
        ILogger<IbgeProvider> logger)
    {
        var maxRetryAttempts = Math.Max(1, options.MaxRetryAttempts);
        var baseDelay = TimeSpan.FromMilliseconds(Math.Max(1, options.RetryBaseDelayMs));

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = maxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = baseDelay,
                MaxDelay = MaximumRetryDelay,
                ShouldHandle = arguments => ValueTask.FromResult(IsTransient(arguments.Outcome)),
                DelayGenerator = arguments => ValueTask.FromResult(GetServerRequestedDelay(arguments.Outcome)),
                OnRetry = arguments =>
                {
                    arguments.Outcome.Result?.Dispose();
                    logger.LogWarning(
                        "IBGE request failed with {Failure}. Retrying in {RetryDelayMs} ms (attempt {AttemptNumber} of {MaxAttempts}).",
                        Describe(arguments.Outcome),
                        arguments.RetryDelay.TotalMilliseconds,
                        arguments.AttemptNumber,
                        maxRetryAttempts);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(Math.Max(1, options.HttpTimeoutSeconds)))
            .Build();
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { } response && IsTransient(response.StatusCode);

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static TimeSpan? GetServerRequestedDelay(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Result?.Headers.RetryAfter is not { } retryAfter)
        {
            return null;
        }

        TimeSpan? requested = retryAfter.Delta;
        if (requested is null && retryAfter.Date is { } retryDate)
        {
            requested = retryDate - DateTimeOffset.UtcNow;
        }

        if (requested is not { } delay)
        {
            return null;
        }

        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > MaximumRetryDelay ? MaximumRetryDelay : delay;
    }

    private static string Describe(Outcome<HttpResponseMessage> outcome) =>
        outcome.Result is { } response
            ? ((int)response.StatusCode).ToString()
            : outcome.Exception?.GetType().Name ?? "unknown";
}
