namespace Bravo.Infrastructure.Tse.Downloads;

public sealed class TseTransientException(
    string message,
    TimeSpan? retryAfter = null,
    Exception? innerException = null
)
    : Exception(message, innerException)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
