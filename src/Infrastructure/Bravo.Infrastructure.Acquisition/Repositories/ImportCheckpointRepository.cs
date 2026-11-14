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
            .Where(x => x.ExecutionId == executionId && x.DatasetKey == datasetKey)
            .OrderByDescending(x => x.CheckpointedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task Save(ImportCheckpoint checkpoint, CancellationToken ct = default)
    {
        await context.ImportCheckpoints.AddAsync(checkpoint, ct);
    }
}

