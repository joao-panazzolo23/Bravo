using Bravo.Domain.Geography.Entities;

namespace Bravo.Application.Electoral.Contracts;

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

    Task<int> UpsertStatesAsync(
        IReadOnlyCollection<State> states,
        CancellationToken cancellationToken = default);
}
