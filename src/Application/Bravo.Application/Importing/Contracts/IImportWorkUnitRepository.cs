using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Contracts;

public interface IImportWorkUnitRepository
{
    Task<ImportWorkUnit?> ClaimNextAsync(
        Guid jobId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        Guid workUnitId,
        string workerId,
        long fencingToken,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<int> RecordAttemptAsync(
        Guid workUnitId,
        long fencingToken,
        string error,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken = default);

    Task<bool> FailAsync(
        Guid jobId,
        Guid workUnitId,
        long fencingToken,
        string error,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(
        Guid workUnitId,
        string workerId,
        long fencingToken,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the work unit completed after its pipeline has persisted all of its work.
    /// </summary>
    Task<bool> CompleteAsync(
        Guid workUnitId,
        long fencingToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a progress snapshot for the unit. Fenced, so a unit that lost ownership
    /// cannot overwrite another worker's counters.
    /// </summary>
    Task<bool> ReportProgressAsync(
        Guid workUnitId,
        long fencingToken,
        ImportStage stage,
        long processedRecords,
        long insertedRecords,
        long updatedRecords,
        long rejectedRecords,
        long? totalRecords,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportWorkUnit>> GetByJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetFailedKeysAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
