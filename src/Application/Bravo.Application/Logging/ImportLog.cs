using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static partial class ImportLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Import job {JobId} started. Source: {Source}, Scope: {Scope}, Attempt: {Attempt}.")]
    public static partial void JobStarted(
        ILogger logger,
        Guid jobId,
        string source,
        string scope,
        int attempt);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Import job {JobId} completed. Processed: {ProcessedRecords}, Failed: {FailedRecords}.")]
    public static partial void JobCompleted(
        ILogger logger,
        Guid jobId,
        long? processedRecords,
        long? failedRecords);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Import job {JobId} completed with errors. Processed: {ProcessedRecords}, Failed: {FailedRecords}.")]
    public static partial void JobCompletedWithErrors(
        ILogger logger,
        Guid jobId,
        long? processedRecords,
        long? failedRecords);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Import job {JobId} was cancelled.")]
    public static partial void JobCancelled(
        ILogger logger,
        Guid jobId);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Import job {JobId} execution stopped.")]
    public static partial void JobStopped(
        ILogger logger,
        Guid jobId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Import job {JobId} released for restart after shutdown.")]
    public static partial void JobReleased(
        ILogger logger,
        Guid jobId);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Error,
        Message = "Import job {JobId} failed. Source: {Source}, Attempt: {Attempt}/{MaxAttempts}, RetryAt: {RetryAt}.")]
    public static partial void JobFailed(
        ILogger logger,
        Guid jobId,
        string source,
        int attempt,
        int maxAttempts,
        DateTime? retryAt,
        Exception exception);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Work unit {WorkUnitId} ({UnitKey}) started.")]
    public static partial void WorkUnitStarted(
        ILogger logger,
        Guid workUnitId,
        string unitKey);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Work unit {WorkUnitId} ({UnitKey}) completed. Processed: {ProcessedRecords} records.")]
    public static partial void WorkUnitCompleted(
        ILogger logger,
        Guid workUnitId,
        string unitKey,
        long processedRecords);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Work unit {WorkUnitId} ({UnitKey}) recovered from checkpoint.")]
    public static partial void WorkUnitRecovered(
        ILogger logger,
        Guid workUnitId,
        string unitKey);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Warning,
        Message = "Work unit {WorkUnitId} ({UnitKey}) retried after failure. Attempt: {Attempt}.")]
    public static partial void WorkUnitRetried(
        ILogger logger,
        Guid workUnitId,
        string unitKey,
        int attempt,
        Exception exception);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Error,
        Message = "Work unit {WorkUnitId} ({UnitKey}) definitively failed.")]
    public static partial void WorkUnitFailed(
        ILogger logger,
        Guid workUnitId,
        string unitKey,
        Exception exception);
}
