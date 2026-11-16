namespace Bravo.Infrastructure.Tse.Downloads;

public sealed class TseDownloadException(string message, Exception? innerException = null)
    : Exception(message, innerException);
