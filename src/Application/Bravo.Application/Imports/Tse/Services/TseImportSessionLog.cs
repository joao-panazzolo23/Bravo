using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Diagnostics for the TSE import session, kept next to the type that owns the behaviour.
/// </summary>
public static partial class TseImportSessionLog
{
    [LoggerMessage(
        EventId = 1230,
        Level = LogLevel.Warning,
        Message = "Could not delete TSE temporary directory '{Directory}'.")]
    public static partial void TemporaryDirectoryCleanupFailed(
        ILogger logger,
        string directory,
        Exception exception);
}
