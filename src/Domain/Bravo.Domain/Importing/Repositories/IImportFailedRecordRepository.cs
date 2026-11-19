using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Domain.Importing.Repositories;

public interface IImportFailedRecordRepository
{
    Task<IReadOnlyList<ImportFailedRecord>> ListByJobAsync(
        Guid jobId,
        int limit,
        CancellationToken cancellationToken = default);
}
