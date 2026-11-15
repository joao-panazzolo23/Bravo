using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;

namespace Bravo.Application.Acquisition.Repositories;

public interface IImportExecutionRepository
{
    Task<ImportExecution?> GetById(Guid id, CancellationToken ct = default);
    Task<ImportExecution?> GetRunningByType(ImportOperationType type, string? scope, CancellationToken ct = default);
    Task<IReadOnlyList<ImportExecution>> GetByStatus(ImportStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetDispatchableIds(CancellationToken ct = default);
    Task Add(ImportExecution execution, CancellationToken ct = default);
    Task Update(ImportExecution execution, CancellationToken ct = default);

    /// <summary>
    /// Atomically claims an execution for the current process by acquiring its lease.
    /// Returns false when another instance holds a valid lease or the execution is finished.
    /// </summary>
    Task<bool> TryClaim(Guid executionId, TimeSpan leaseDuration, CancellationToken ct = default);

    /// <summary>
    /// Extends the lease of an execution owned by the current process.
    /// </summary>
    Task<bool> RenewLease(Guid executionId, TimeSpan leaseDuration, CancellationToken ct = default);

    /// <summary>
    /// Reads the current status directly from PostgreSQL (used to observe cancellation).
    /// </summary>
    Task<ImportStatus?> GetStatus(Guid executionId, CancellationToken ct = default);
}
