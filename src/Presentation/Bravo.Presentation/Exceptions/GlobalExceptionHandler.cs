using Bravo.Presentation.Exceptions.Strategies;
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

        var handler = SelectStrategy(_handlers, exception);

        if (handler is null) return false;

        return await handler.Handle(httpContext, exception, cancellationToken);
    }

    /// <summary>
    /// Picks a strategy, deprioritising the catch-all system strategy.
    /// </summary>
    /// <remarks>
    /// <see cref="ArgumentException"/> is a <see cref="SystemException"/>, so both the argument
    /// strategy and the system strategy can handle it. The system strategy is the last resort for
    /// unexpected failures and answers 500; letting it win would turn a client error into a server
    /// error depending on DI registration order. Ranking it last makes the choice deterministic.
    /// </remarks>
    private static IExceptionStrategy? SelectStrategy(
        IEnumerable<IExceptionStrategy> strategies,
        Exception exception)
        => strategies
            .Where(strategy => strategy.CanHandle(exception))
            .OrderBy(strategy => strategy is SystemExceptionStrategy)
            .FirstOrDefault();
}
