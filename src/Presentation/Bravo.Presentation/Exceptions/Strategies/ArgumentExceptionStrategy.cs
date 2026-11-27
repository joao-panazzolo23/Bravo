using Bravo.Presentation.Exceptions.Models;
using Bravo.Presentation.Exceptions.Strategies.Abstract;
using System.Net;

namespace Bravo.Presentation.Exceptions.Strategies;

/// <summary>
/// Maps an <see cref="ArgumentException"/> to a 400 rather than a 500.
/// </summary>
/// <remarks>
/// Analytical requests reject invalid filter combinations by throwing <see cref="ArgumentException"/>
/// from the request contract (for example, a share analysis without an office). That is a client
/// error: the request was well formed but cannot produce a meaningful answer. Without this strategy
/// the generic handler would answer 500, which wrongly suggests the server is broken.
/// </remarks>
public class ArgumentExceptionStrategy : IExceptionStrategy
{
    public bool CanHandle(Exception exception) => exception is ArgumentException;

    public async Task<bool> Handle(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var response = new ErrorResult(
            statusCode: HttpStatusCode.BadRequest,
            type: "InvalidRequest",
            details: exception.Message);

        await context.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
