using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Services;

public interface IImportBatchStore<TRecord>
{
    Task PersistBatchAsync(
        ImportBatchWriteContext context,
        IReadOnlyList<TRecord> records,
        IReadOnlyList<ImportRejectedRecord> rejectedRecords,
        CancellationToken cancellationToken = default);
}
