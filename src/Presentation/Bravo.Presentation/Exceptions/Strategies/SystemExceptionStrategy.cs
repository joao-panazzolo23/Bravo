using Bravo.Presentation.Exceptions.Models;
using Bravo.Presentation.Exceptions.Strategies.Abstract;
using System.Net;

namespace Bravo.Presentation.Exceptions.Strategies;

public class SystemExceptionStrategy : IExceptionStrategy
{
    public bool CanHandle(Exception exception) => exception is SystemException;

    public async Task<bool> Handle(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
        )
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response =
            new ErrorResult(
                statusCode: HttpStatusCode.InternalServerError,
                type: "SystemException",
                details: exception.Message
                );

        await context.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
