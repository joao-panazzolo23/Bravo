namespace Bravo.Application.Exceptions;

public abstract class ApiExceptionBase(IEnumerable<ApiError> errors) : Exception
{
    public IEnumerable<ApiError> Errors { get; } = errors;

    public virtual string Type { get; } = typeof(ApiExceptionBase).GetType().Name;
}
