namespace Bravo.Application.Tse.Models;

/// <summary>
/// Immmutable identifier maps for one state's electoral structure, loaded after the structure
/// upserts for a scope complete.
/// </summary>
/// <remarks>
/// Zones, sections and polling places are keyed by TSE numbers that are only unique within a
/// state, so every map is state-scoped. One snapshot is shared by the structure-ensuring code
/// and the row resolver, which removes the duplicate nationwide reads the import used to make.
/// </remarks>
public sealed class TseStructureIds(
    IReadOnlyDictionary<(Guid StateId, int Number), Guid> zoneIds,
    IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid> sectionIds,
    IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid> pollingPlaceIds)
{
    public IReadOnlyDictionary<(Guid StateId, int Number), Guid> ZoneIds { get; } = zoneIds;

    public IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid> SectionIds { get; } = sectionIds;

    public IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid> PollingPlaceIds { get; } = pollingPlaceIds;
}
