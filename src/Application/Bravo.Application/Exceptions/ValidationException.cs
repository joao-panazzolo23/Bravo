namespace Bravo.Application.Exceptions;

public sealed class ValidationException : ApiExceptionBase
{
    public ValidationException(List<ApiError> errors)
        : base(errors) { }
}

