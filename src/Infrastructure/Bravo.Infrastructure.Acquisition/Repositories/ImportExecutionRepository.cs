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
            .AsNoTracking()
            .Where(x =>
                x.OperationType == type &&
                x.Scope == scope &&
                (x.Status == ImportStatus.Running
                 || x.Status == ImportStatus.Pending
                 || x.Status == ImportStatus.CancellationRequested))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ImportExecution>> GetByStatus(ImportStatus status, CancellationToken ct = default)
    {
        return await context.ImportExecutions
            .Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDispatchableIds(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Executions that were left unfinished by a previous process. The lease check is part
        // of the query so a live instance in another process is never stolen.
        var ids = await context.ImportExecutions
            .AsNoTracking()
            .Where(x =>
                (x.Status == ImportStatus.Pending
                 || x.Status == ImportStatus.Running
                 || x.Status == ImportStatus.CancellationRequested)
                && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync(ct);

        return ids;
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

    public async Task<bool> TryClaim(Guid executionId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(leaseDuration);

        // Conditional update: only one process can move the lease forward from an expired or
        // absent value. This is the authoritative overlap protection across instances.
        var affected = await context.ImportExecutions
            .Where(x => x.Id == executionId
                        && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now)
                        && (x.Status == ImportStatus.Pending
                            || x.Status == ImportStatus.Running
                            || x.Status == ImportStatus.CancellationRequested))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LeaseExpiresAt, expires)
                .SetProperty(x => x.HeartbeatAt, now), ct);

        return affected == 1;
    }

    public async Task<bool> RenewLease(Guid executionId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(leaseDuration);

        var affected = await context.ImportExecutions
            .Where(x => x.Id == executionId
                        && (x.Status == ImportStatus.Running
                            || x.Status == ImportStatus.Pending
                            || x.Status == ImportStatus.CancellationRequested))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LeaseExpiresAt, expires)
                .SetProperty(x => x.HeartbeatAt, now), ct);

        return affected == 1;
    }

    public async Task<ImportStatus?> GetStatus(Guid executionId, CancellationToken ct = default)
    {
        return await context.ImportExecutions
            .AsNoTracking()
            .Where(x => x.Id == executionId)
            .Select(x => (ImportStatus?)x.Status)
            .FirstOrDefaultAsync(ct);
    }
}
