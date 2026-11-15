using Bravo.Domain.Importing.Entities;

namespace Bravo.Application.Importing.Contracts;

public interface IImportFailedRecordRepository
{
    Task<IReadOnlyList<ImportFailedRecord>> ListByJobAsync(
        Guid jobId,
        int limit,
        CancellationToken cancellationToken = default);
}
