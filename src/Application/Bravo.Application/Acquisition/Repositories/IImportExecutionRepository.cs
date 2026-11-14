using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;

namespace Bravo.Application.Acquisition.Repositories;

public interface IImportExecutionRepository
{
    Task<ImportExecution?> GetById(Guid id, CancellationToken ct = default);
    Task<ImportExecution?> GetRunningByType(ImportOperationType type, string? scope, CancellationToken ct = default);
    Task<IReadOnlyList<ImportExecution>> GetByStatus(ImportStatus status, CancellationToken ct = default);
    Task Add(ImportExecution execution, CancellationToken ct = default);
    Task Update(ImportExecution execution, CancellationToken ct = default);
}
