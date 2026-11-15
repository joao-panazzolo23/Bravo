using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Contracts;

public interface IImportSource<TRecord>
{
    Task<ImportSourceBatch<TRecord>> ReadBatchAsync(
        string? cursor,
        int batchSize,
        CancellationToken cancellationToken = default);
}
