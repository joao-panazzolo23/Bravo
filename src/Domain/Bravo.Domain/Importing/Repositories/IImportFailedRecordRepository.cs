using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Repositories;

public interface IImportFailedRecordRepository
{
    Task<IReadOnlyList<ImportFailedRecord>> ListByJobAsync(
        Guid jobId,
        int limit,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Guid jobId,
        IReadOnlyCollection<ImportRejectedRecord> records,
        CancellationToken cancellationToken = default);
}
