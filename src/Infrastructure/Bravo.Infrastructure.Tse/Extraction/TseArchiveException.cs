namespace Bravo.Infrastructure.Tse.Extraction;

public sealed class TseArchiveException(string message, Exception? innerException = null)
    : Exception(message, innerException);
