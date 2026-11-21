using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Repositories;

public interface IImportJobRepository
{
    Task<bool> CreateAsync(ImportJob job, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<ImportJob?> ClaimAsync(
        Guid id,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(
        Guid id,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> ReportProgressAsync(
        Guid id,
        string workerId,
        ImportJobProgress progress,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid id,
        string workerId,
        ImportJobStatus status,
        string? error,
        CancellationToken cancellationToken = default);
    Task FailAsync(
        Guid id,
        string workerId,
        string error,
        DateTime? retryAt,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReleaseAsync(
        Guid id,
        string workerId,
        string? reason,
        CancellationToken cancellationToken = default);
    Task<int> ReleaseOwnedByAsync(
        string ownerPrefix,
        CancellationToken cancellationToken = default);
}
