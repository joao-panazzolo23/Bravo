using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Contracts;

public interface ICoalitionRepository
{
    Task<int> UpsertAsync(
        IReadOnlyCollection<Coalition> coalitions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid RoundId, Guid OfficeId, long Number), Guid>> GetIdsAsync(
        CancellationToken cancellationToken = default);
}
