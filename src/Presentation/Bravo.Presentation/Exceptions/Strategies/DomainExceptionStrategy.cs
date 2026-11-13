using Bravo.Presentation.Exceptions.Models;
using Bravo.Presentation.Exceptions.Strategies.Abstract;
using Bravo.Shared.Exceptions;
using System.Net;

namespace Bravo.Presentation.Exceptions.Strategies;

public class DomainExceptionStrategy : IExceptionStrategy
{
    public bool CanHandle(Exception exception)
    {
        return exception is DomainException;
    }

    public async Task<bool> Handle(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var result = new ErrorResult(
            statusCode: HttpStatusCode.BadRequest,
            "Domain",
            ((DomainException)exception).Error
            );

        await context.Response.WriteAsJsonAsync(
            result,
            cancellationToken);

        return true;
    }
}
