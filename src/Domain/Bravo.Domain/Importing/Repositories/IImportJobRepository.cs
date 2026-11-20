using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Repositories;

/// <summary>
/// Durable job queue. The worker claims a job, keeps its lease alive while it runs, and
/// reports progress against the job row.
/// </summary>
public interface IImportJobRepository
{
    /// <summary>
    /// Inserts the job unless a Pending/Running job already covers the same source and scope.
    /// </summary>
    Task<bool> CreateAsync(ImportJob job, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically takes ownership of a recoverable job and resets its progress for a fresh run.
    /// Returns null when another worker won the claim.
    /// </summary>
    Task<ImportJob?> ClaimAsync(
        Guid id,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the lease. Returns false when the job is no longer running under this owner,
    /// which is how a worker learns that it was cancelled or superseded.
    /// </summary>
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

    /// <summary>
    /// Marks the job failed, or schedules another attempt when <paramref name="retryAt"/> is set.
    /// </summary>
    Task FailAsync(
        Guid id,
        string workerId,
        string error,
        DateTime? retryAt,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a Failed job back to Pending so the worker runs it again from the start. Returns
    /// false when the job is not failed or an overlapping job is already active.
    /// </summary>
    Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an owned job to Pending after a graceful shutdown so another worker (or this
    /// one after restart) can run it again.
    /// </summary>
    Task ReleaseAsync(
        Guid id,
        string workerId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases jobs whose lease owner starts with <paramref name="ownerPrefix"/>. Used at
    /// startup to recover from an unclean shutdown without waiting for the lease to expire.
    /// </summary>
    Task<int> ReleaseOwnedByAsync(
        string ownerPrefix,
        CancellationToken cancellationToken = default);
}
