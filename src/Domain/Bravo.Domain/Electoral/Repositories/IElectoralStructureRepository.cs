using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface IElectoralStructureRepository
{
    Task<int> UpsertZonesAsync(
        IReadOnlyCollection<ElectoralZone> zones,
        CancellationToken cancellationToken = default
    );

    Task<int> UpsertSectionsAsync(
        IReadOnlyCollection<ElectoralSection> sections,
        CancellationToken cancellationToken = default
    );

    Task<int> UpsertPollingPlacesAsync(
        IReadOnlyCollection<PollingPlace> places,
        CancellationToken cancellationToken = default
    );

    Task<int> UpsertAssignmentsAsync(
        IReadOnlyCollection<PollingPlaceSection> assignments,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<(Guid StateId, int Number), Guid>> GetZoneIdsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid>> GetSectionIdsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid>> GetPollingPlaceIdsAsync(
        CancellationToken cancellationToken = default
    );
}
