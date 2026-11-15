using Bravo.Domain.Acquisition.Entities;

namespace Bravo.Application.Acquisition.Repositories;

public interface IImportRejectionRepository
{
    Task AddRange(IReadOnlyList<ImportRejection> rejections, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of rejections already persisted for an execution.
    /// </summary>
    Task<int> Count(Guid executionId, CancellationToken ct = default);
}
