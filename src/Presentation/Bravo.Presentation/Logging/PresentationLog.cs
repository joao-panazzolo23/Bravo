using Microsoft.Extensions.Logging;

namespace Bravo.Presentation.Logging;

public static partial class PresentationLog
{
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Error,
        Message = "Unhandled exception registered: {Message}.")]
    public static partial void UnhandledException(
        ILogger logger,
        string message,
        Exception exception);
}
