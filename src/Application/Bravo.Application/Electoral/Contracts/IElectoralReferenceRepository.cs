using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Contracts;

public interface IElectoralReferenceRepository
{
    Task<int> UpsertOfficesAsync(
        IReadOnlyCollection<ElectoralOffice> offices,
        CancellationToken cancellationToken = default
    );

    Task<int> UpsertPartiesAsync(
        IReadOnlyCollection<PoliticalParty> parties,
        CancellationToken cancellationToken = default
    );

    Task<int> UpsertFederationsAsync(
        IReadOnlyCollection<Federation> federations,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, Guid>> GetOfficeIdsAsync(
        CancellationToken cancellationToken = default
    );

    //todo: using tuples at interfaces are almost an antipattern
    Task<IReadOnlyDictionary<(int Number, string Acronym), Guid>> GetPartyIdsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetFederationIdsAsync(
        CancellationToken cancellationToken = default
    );
}
