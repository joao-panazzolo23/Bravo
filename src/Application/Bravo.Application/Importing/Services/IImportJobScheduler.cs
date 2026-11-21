using Bravo.Domain.Importing.Repositories;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Read-only view of the durable queue that the scheduler needs.
/// </summary>
/// <remarks>
/// The scheduler only needs to know which jobs are eligible to run. Keeping that behind a
/// dedicated abstraction stops the hosted service from depending on the whole job repository
/// and makes the dispatch loop trivially fakeable in tests.
/// </remarks>
public interface IImportJobScheduler
{
    Task<IReadOnlyList<Guid>> ListRecoverableAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class ImportJobScheduler(IImportJobRepository jobs) : IImportJobScheduler
{
    public Task<IReadOnlyList<Guid>> ListRecoverableAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => jobs.ListRecoverableIdsAsync(limit, cancellationToken);
}
