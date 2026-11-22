namespace Bravo.Application.Imports.Common.Exceptions;

public sealed class ImportPermanentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
