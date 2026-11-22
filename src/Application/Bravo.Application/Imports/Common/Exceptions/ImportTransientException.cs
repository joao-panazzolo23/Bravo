namespace Bravo.Application.Imports.Common.Exceptions;

public sealed class ImportTransientException(
    string message,
    TimeSpan? retryAfter = null,
    Exception? innerException = null
) : Exception(message, innerException)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
