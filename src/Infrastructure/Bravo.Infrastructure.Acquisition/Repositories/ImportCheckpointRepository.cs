using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Bravo.Infrastructure.Acquisition.Repositories;

public sealed class ImportCheckpointRepository(PostgreSQLContext context) : IImportCheckpointRepository
{
    public async Task<ImportCheckpoint?> GetLatest(Guid executionId, string datasetKey, CancellationToken ct = default)
    {
        return await context.ImportCheckpoints
            .AsNoTracking()
            .Where(x => x.ExecutionId == executionId && x.DatasetKey == datasetKey)
            .OrderByDescending(x => x.CheckpointedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlySet<string>> GetCompletedKeys(Guid executionId, CancellationToken ct = default)
    {
        var keys = await context.ImportCheckpoints
            .AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .Select(x => x.DatasetKey)
            .ToListAsync(ct);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task Save(ImportCheckpoint checkpoint, CancellationToken ct = default)
    {
        await context.ImportCheckpoints.AddAsync(checkpoint, ct);
    }
}
