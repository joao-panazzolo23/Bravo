using Bravo.Application.Exceptions;
using Bravo.Presentation.Exceptions.Models;
using Bravo.Presentation.Exceptions.Strategies.Abstract;

namespace Bravo.Presentation.Exceptions.Strategies;

public class ApplicationExceptionStrategy : IExceptionStrategy
{
    public bool CanHandle(Exception exception) => exception is ValidationException;

    async Task<bool> IExceptionStrategy.Handle(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
        )
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var response = new ErrorResult(
            System.Net.HttpStatusCode.BadRequest,
            "Application",
            ((ValidationException)exception).Errors);

        await context.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
