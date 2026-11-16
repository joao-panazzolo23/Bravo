using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Contracts;

public interface IElectionResultRepository
{
    Task<int> UpsertResultsAsync(
        IReadOnlyCollection<ElectionResult> results,
        CancellationToken cancellationToken = default);
}
