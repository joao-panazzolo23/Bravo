namespace Bravo.Application.Importing.Exceptions;

public sealed class ImportPermanentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
