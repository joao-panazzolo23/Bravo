using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Bravo.Infrastructure.Acquisition.Repositories;

public sealed class ImportExecutionRepository(PostgreSQLContext context) : IImportExecutionRepository
{
    public async Task<ImportExecution?> GetById(Guid id, CancellationToken ct = default)
    {
        return await context.ImportExecutions
            .Include(x => x.Checkpoints)
            .Include(x => x.Provenances)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<ImportExecution?> GetRunningByType(ImportOperationType type, string? scope, CancellationToken ct = default)
    {
        return await context.ImportExecutions
            .FirstOrDefaultAsync(x =>
                x.OperationType == type &&
                x.Scope == scope &&
                (x.Status == ImportStatus.Running || x.Status == ImportStatus.Pending || x.Status == ImportStatus.CancellationRequested),
                ct);
    }

    public async Task<IReadOnlyList<ImportExecution>> GetByStatus(ImportStatus status, CancellationToken ct = default)
    {
        return await context.ImportExecutions
            .Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task Add(ImportExecution execution, CancellationToken ct = default)
    {
        await context.ImportExecutions.AddAsync(execution, ct);
    }

    public Task Update(ImportExecution execution, CancellationToken ct = default)
    {
        context.ImportExecutions.Update(execution);
        return Task.CompletedTask;
    }
}

