using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;

namespace Bravo.Domain.Geography.Repositories;

public sealed record MunicipalityLookup(
    Guid Id,
    Guid StateId,
    string Name,
    int IbgeCode
);

/// <summary>
/// Read access to geographic reference data plus the upsert needed to seed synthetic scopes.
/// </summary>
public interface IGeographyRepository
{
    Task<IReadOnlyDictionary<string, Guid>> GetStateIdsByAcronymAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetMunicipalityIdsByIbgeCodeAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the municipalities of a single state. Callers resolving one state's records must
    /// use this overload so a nationwide table is never loaded per import scope.
    /// </summary>
    Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
        Guid stateId,
        CancellationToken cancellationToken = default);

    Task<int> UpsertStatesAsync(
        IReadOnlyCollection<State> states,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts municipalities, resolving each one's state by IBGE code. Returns the number of
    /// rows written; a count below the input means some states were not imported yet.
    /// </summary>
    Task<int> UpsertMunicipalitiesAsync(
        IReadOnlyCollection<MunicipalityUpsert> municipalities,
        CancellationToken cancellationToken = default);
}
