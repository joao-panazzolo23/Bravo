using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Contracts;

public interface IImportBatchStore<TRecord>
{
    Task PersistBatchAsync(
        ImportBatchWriteContext context,
        IReadOnlyList<TRecord> records,
        IReadOnlyList<ImportRejectedRecord> rejectedRecords,
        CancellationToken cancellationToken = default);
}
