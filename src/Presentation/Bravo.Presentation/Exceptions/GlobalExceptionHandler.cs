using Bravo.Presentation.Exceptions.Strategies.Abstract;
using Bravo.Presentation.Logging;
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
        PresentationLog.UnhandledException(_logger, exception.Message, exception);

        var handler = _handlers.First(x => x.CanHandle(exception));

        if (handler is null) return false;

        return await handler!.Handle(httpContext, exception, cancellationToken);
    }
}
