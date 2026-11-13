namespace Bravo.Application.Exceptions;

public abstract class ApiExceptionBase : Exception
{
    public IEnumerable<ApiError> Errors { get; }

    protected ApiExceptionBase(IEnumerable<ApiError> errors)
    {
        Errors = errors;
    }

    public virtual string Type { get; } = typeof(ApiExceptionBase).GetType().Name;
}
