using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;

namespace Bravo.Infrastructure.Acquisition.Repositories;

public sealed class ImportRejectionRepository(PostgreSQLContext context) : IImportRejectionRepository
{
    public async Task AddRange(IReadOnlyList<ImportRejection> rejections, CancellationToken ct = default)
    {
        if (rejections.Count == 0)
        {
            return;
        }

        await context.ImportRejections.AddRangeAsync(rejections, ct);
    }

    public Task<int> Count(Guid executionId, CancellationToken ct = default) =>
        context.ImportRejections
            .AsNoTracking()
            .CountAsync(x => x.ExecutionId == executionId, ct);
}
