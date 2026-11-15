namespace Bravo.Application.Importing.Exceptions;

public sealed class ImportTransientException : Exception
{
    public TimeSpan? RetryAfter { get; }

    public ImportTransientException(string message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }
}
