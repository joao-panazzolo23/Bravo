using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface IElectionResultRepository
{
    Task<int> UpsertResultsAsync(
        IReadOnlyCollection<ElectionResult> results,
        CancellationToken cancellationToken = default
    );
}
