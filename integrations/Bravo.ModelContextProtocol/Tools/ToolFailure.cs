using Bravo.Application.Exceptions;
using ModelContextProtocol;

namespace Bravo.ModelContextProtocol.Tools;

/// <summary>
/// Turns an analytical failure into an MCP error that carries the reason.
/// </summary>
/// <remarks>
/// The MCP SDK replaces the message of any unhandled exception with a generic "An error occurred
/// invoking ..." text, which hides the actual cause from the client. Only <see cref="McpException"/>
/// keeps its message. The failures this helper converts are the ones a caller can act on: an invalid
/// request (validation) or a filter combination the domain refuses (for example a share analysis
/// without an office). Unexpected exceptions are not converted, so genuine defects remain visible in
/// logs instead of being dressed up as user errors.
/// </remarks>
internal static class ToolFailure
{
    public static async Task<T> WrapAsync<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (ValidationException exception)
        {
            throw new McpException(exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}
