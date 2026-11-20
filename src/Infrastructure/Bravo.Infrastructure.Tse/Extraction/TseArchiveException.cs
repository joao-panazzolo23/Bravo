namespace Bravo.Infrastructure.Tse.Extraction;

internal sealed class TseArchiveException(string message, Exception? innerException = null)
    : Exception(message, innerException);
