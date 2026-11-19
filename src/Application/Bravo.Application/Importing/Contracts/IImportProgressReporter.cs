using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Contracts;

/// <summary>
/// Receives progress from a running pipeline.
/// </summary>
/// <remarks>
/// Calls are awaited per batch, never per record. Implementations must throttle so that a fast
/// pipeline produces a bounded number of writes.
/// </remarks>
public interface IImportProgressReporter
{
    /// <summary>Marks the pipeline stage the unit is entering.</summary>
    Task StageAsync(ImportStage stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports cumulative record counters for the unit.
    /// </summary>
    /// <param name="totalRecords">
    /// Total declared by the source, or null when unknown. Never pass zero to mean "unknown".
    /// </param>
    Task RecordsAsync(
        long processed,
        long inserted,
        long updated,
        long rejected,
        long? totalRecords = null,
        CancellationToken cancellationToken = default);

    /// <summary>Flushes any pending counters, used when a unit finishes or fails.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
