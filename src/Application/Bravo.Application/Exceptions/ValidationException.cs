namespace Bravo.Application.Exceptions;

public sealed class ValidationException(List<ApiError> errors) : ApiExceptionBase(errors)
{
}
