using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Services;

public interface IImportSource<TRecord>
{
    Task<ImportSourceBatch<TRecord>> ReadBatchAsync(
        string? cursor,
        int batchSize,
        CancellationToken cancellationToken = default);
}
