using Bravo.Domain.Importing.Entities;

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

    Task<IReadOnlyList<ImportWorkUnit>> GetByJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetFailedKeysAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
