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

    /// <summary>
    /// Reads the zone identifiers for one state. TSE zone numbers are only unique within a
    /// state, so the caller must supply the state it is importing.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid StateId, int Number), Guid>> GetZoneIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Reads the section identifiers for one state, joined through its zones.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid>> GetSectionIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Reads the polling-place identifiers for one state.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid>> GetPollingPlaceIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default
    );
}
