using Bravo.Domain.Importing.Entities;

namespace Bravo.Application.Importing.Contracts;

public interface IImportJobRepository
{
    Task<bool> CreateAsync(
        ImportJob job,
        IReadOnlyCollection<ImportWorkUnit> workUnits,
        CancellationToken cancellationToken = default);

    Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> MarkRunningAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsCancellationRequestedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    Task FailAsync(Guid id, string error, CancellationToken cancellationToken = default);
}
