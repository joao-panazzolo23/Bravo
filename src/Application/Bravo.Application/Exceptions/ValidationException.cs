namespace Bravo.Application.Exceptions;

/// <summary>
/// Raised when a request fails validation.
/// </summary>
/// <remarks>
/// The individual failures live in <see cref="ApiExceptionBase.Errors"/>, which the HTTP pipeline
/// serialises as a structured payload. The exception message is also populated because transports
/// that only surface an exception's message — notably the MCP server, which renders a tool failure
/// as text — would otherwise report "an error occurred" without saying what was rejected.
/// </remarks>
public sealed class ValidationException(List<ApiError> errors)
    : ApiExceptionBase(errors, Describe(errors))
{
    private static string Describe(List<ApiError> errors)
    {
        if (errors.Count == 0)
            return "Validation failed.";

        var details = errors.Select(error => $"{error.Field}: {error.Error}");

        return $"Validation failed: {string.Join("; ", details)}";
    }
}
