using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static partial class WorkerLog
{
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Worker {WorkerId} started. Poll interval: {PollIntervalSeconds}s, Max concurrent jobs: {MaxConcurrentJobs}.")]
    public static partial void WorkerStarted(
        ILogger logger,
        string workerId,
        int pollIntervalSeconds,
        int maxConcurrentJobs);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Worker {WorkerId} stopped.")]
    public static partial void WorkerStopped(
        ILogger logger,
        string workerId);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Import worker is disabled by configuration.")]
    public static partial void WorkerDisabled(
        ILogger logger);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Debug,
        Message = "Worker {WorkerId} is at capacity or idle with {RunningJobs} job(s) in flight.")]
    public static partial void WorkerIdle(
        ILogger logger,
        string workerId,
        int runningJobs);

    [LoggerMessage(
        EventId = 1105,
        Level = LogLevel.Error,
        Message = "Worker {WorkerId} could not claim import job {JobId}.")]
    public static partial void WorkerClaimFailed(
        ILogger logger,
        string workerId,
        Guid jobId,
        Exception exception);

    [LoggerMessage(
        EventId = 1106,
        Level = LogLevel.Warning,
        Message = "Import job {JobId} is no longer owned by worker {WorkerId}; stopping execution.")]
    public static partial void WorkerLeaseLost(
        ILogger logger,
        Guid jobId,
        string workerId);

    [LoggerMessage(
        EventId = 1107,
        Level = LogLevel.Error,
        Message = "Heartbeat failed for import job {JobId}.")]
    public static partial void WorkerHeartbeatFailed(
        ILogger logger,
        Guid jobId,
        Exception exception);

    [LoggerMessage(
        EventId = 1108,
        Level = LogLevel.Warning,
        Message = "Could not renew lease for import job {JobId}.")]
    public static partial void WorkerLeaseRenewFailed(
        ILogger logger,
        Guid jobId,
        Exception exception);

    [LoggerMessage(
        EventId = 1109,
        Level = LogLevel.Warning,
        Message = "Recovered {Count} import job(s) abandoned by a previous process on {MachineName}.")]
    public static partial void WorkerAbandonedJobsRecovered(
        ILogger logger,
        int count,
        string machineName);

    [LoggerMessage(
        EventId = 1110,
        Level = LogLevel.Error,
        Message = "Import worker polling failed.")]
    public static partial void WorkerPollingFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1111,
        Level = LogLevel.Error,
        Message = "Import worker could not release abandoned jobs at startup.")]
    public static partial void WorkerReleaseAbandonedFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1112,
        Level = LogLevel.Error,
        Message = "Import job {JobId} references unregistered source '{Source}'.")]
    public static partial void UnregisteredSource(
        ILogger logger,
        Guid jobId,
        string source);
}
