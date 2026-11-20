using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface ICoalitionRepository
{
    Task<int> UpsertAsync(
        IReadOnlyCollection<Coalition> coalitions,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Reads the coalition identifiers for one state. Coalition numbers only repeat within a
    /// state, so a caller importing one state must never scan the whole table.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid RoundId, Guid OfficeId, long Number), Guid>> GetIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default
    );
}
