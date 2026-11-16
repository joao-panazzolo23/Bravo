namespace Bravo.Application.Electoral.Contracts;

public sealed record MunicipalityLookup(Guid Id, Guid StateId, string Name, int IbgeCode);

public interface IGeographyLookup
{
    Task<IReadOnlyDictionary<string, Guid>> GetStateIdsByAcronymAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetMunicipalityIdsByIbgeCodeAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
        CancellationToken cancellationToken = default);
}
