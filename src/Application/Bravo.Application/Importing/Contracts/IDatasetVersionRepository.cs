using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Contracts;

public interface IDatasetVersionRepository
{
    Task<DatasetVersion> CreateAsync(
        DatasetVersion version,
        CancellationToken cancellationToken = default);

    Task<bool> SetStatusAsync(
        Guid id,
        DatasetVersionStatus status,
        CancellationToken cancellationToken = default);

    Task<DatasetVersion?> GetActiveAsync(
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks <paramref name="id"/> as the only active version for its scope and
    /// supersedes the previously active one.
    /// </summary>
    Task<bool> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
}
