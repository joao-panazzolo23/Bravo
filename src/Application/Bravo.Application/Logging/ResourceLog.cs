using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

/// <summary>
/// Lifecycle events for the shared import resource governor and the bounded persistence
/// pipeline. Contention and backpressure are logged rather than silently absorbed, because they
/// are the symptoms that tell an operator the configured limits are too tight or too loose.
/// </summary>
public static partial class ResourceLog
{
    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Debug,
        Message = "Waited {WaitMs:F0} ms for a '{Resource}' permit.")]
    public static partial void ResourcePermitWaited(
        ILogger logger,
        string resource,
        double waitMs);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Debug,
        Message = "Persistence queue for {Year}/{State} is full; parser is waiting for capacity.")]
    public static partial void PersistenceBackpressure(
        ILogger logger,
        int year,
        string state);

    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Information,
        Message = "Processed {Rows} result rows for {Year}/{State} in {ElapsedMs} ms ({RowsPerSecond:F0} rows/s).")]
    public static partial void ResultScopeCompleted(
        ILogger logger,
        int year,
        string state,
        long rows,
        double elapsedMs,
        double rowsPerSecond);

    [LoggerMessage(
        EventId = 1504,
        Level = LogLevel.Warning,
        Message = "Skipped polling place {PollingPlaceNumber} for {Year}/{State}: it is not a valid location number.")]
    public static partial void InvalidPollingPlaceSkipped(
        ILogger logger,
        int pollingPlaceNumber,
        int year,
        string state);

    [LoggerMessage(
        EventId = 1505,
        Level = LogLevel.Debug,
        Message = "Memory snapshot during import: managed={ManagedMb:F0} MB, workingSet={WorkingSetMb:F0} MB, loh={LohMb:F0} MB, gen2={Gen2}.")]
    public static partial void ImportMemorySnapshot(
        ILogger logger,
        double managedMb,
        double workingSetMb,
        double lohMb,
        int gen2);
}
