using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Bravo.Infrastructure.Acquisition.Repositories;

public sealed class DatasetProvenanceRepository(PostgreSQLContext context) : IDatasetProvenanceRepository
{
    public async Task Add(DatasetProvenance provenance, CancellationToken ct = default)
    {
        await context.DatasetProvenances.AddAsync(provenance, ct);
    }

    public Task Update(DatasetProvenance provenance, CancellationToken ct = default)
    {
        context.DatasetProvenances.Update(provenance);
        return Task.CompletedTask;
    }

    public async Task<DatasetProvenance?> GetLatest(string provider, string datasetIdentifier, CancellationToken ct = default)
    {
        return await context.DatasetProvenances
            .AsNoTracking()
            .Where(x => x.Provider == provider && x.DatasetIdentifier == datasetIdentifier)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DatasetProvenance?> GetLatestSuccessful(string provider, string datasetIdentifier, CancellationToken ct = default)
    {
        return await context.DatasetProvenances
            .AsNoTracking()
            .Where(x => x.Provider == provider
                        && x.DatasetIdentifier == datasetIdentifier
                        && (x.Execution.Status == Domain.Acquisition.Enums.ImportStatus.Completed
                            || x.Execution.Status == Domain.Acquisition.Enums.ImportStatus.CompletedWithErrors))
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(ct);
    }
}
