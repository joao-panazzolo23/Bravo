using Bravo.Presentation.Exceptions.Strategies.Abstract;
using Microsoft.AspNetCore.Diagnostics;

namespace Bravo.Presentation.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> _logger,
    IEnumerable<IExceptionStrategy> _handlers
    ) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
        )
    {
        _logger.LogError($"Exception registered: {exception.Message}", exception);

        var handler = _handlers.First(x => x.CanHandle(exception));

        if (handler is null) return false;

        return await handler!.Handle(httpContext, exception, cancellationToken);
    }
}
