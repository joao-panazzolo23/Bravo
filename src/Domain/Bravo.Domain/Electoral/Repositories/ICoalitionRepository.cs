using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface ICoalitionRepository
{
    Task<int> UpsertAsync(
        IReadOnlyCollection<Coalition> coalitions,
        CancellationToken cancellationToken = default
    );

    //todo: tuple = antipattern
    Task<IReadOnlyDictionary<(Guid RoundId, Guid OfficeId, long Number), Guid>> GetIdsAsync(
        CancellationToken cancellationToken = default
    );
}
