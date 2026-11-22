using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Ensures the electoral structure a results file refers to exists before votes are written.
/// </summary>
/// <remarks>
/// Structure is round-independent, so it is ensured once per (year, state) file. Every upsert is
/// keyed by a natural key, which is what makes re-running an import safe. All reads are scoped to
/// the state being imported, and the resulting maps are returned so the row resolver reuses them
/// instead of reading the tables again.
///
/// The input is <see cref="TseStructureKeys"/> gathered from a first sequential pass over the
/// file, so this stage is proportional to the distinct geography of one state rather than to the
/// number of result rows.
/// </remarks>
public sealed class TseStructurePreparer(IElectoralStructureRepository structure)
{
    public async Task<TseStructureIds> PrepareAsync(
        TseStructureKeys keys,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var zoneIds = await EnsureZonesAsync(
            keys,
            stateId,
            municipalityIds,
            cancellationToken
        );
        var sectionIds = await EnsureSectionsAsync(
            keys,
            stateId,
            municipalityIds,
            zoneIds,
            cancellationToken
        );
        var placeIds = await EnsurePollingPlacesAsync(
            keys,
            stateId,
            municipalityIds,
            zoneIds,
            cancellationToken
        );

        await EnsureAssignmentsAsync(
            keys,
            stateId,
            municipalityIds,
            zoneIds,
            sectionIds,
            placeIds,
            electionLookup,
            cancellationToken
        );

        return new TseStructureIds(zoneIds, sectionIds, placeIds);
    }

    private async Task<IReadOnlyDictionary<(Guid, int), Guid>> EnsureZonesAsync(
        TseStructureKeys keys,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        CancellationToken cancellationToken)
    {
        var zoneIds = await structure.GetZoneIdsAsync(stateId, cancellationToken);

        var newZones = keys.ZoneNumbers()
            .Where(number => !zoneIds.ContainsKey((stateId, number)))
            .Select(number => ElectoralZone.Create(
                stateId,
                number,
                FirstMunicipalityInZone(keys, municipalityIds, number)))
            .ToArray();

        if (newZones.Length == 0)
            return zoneIds;

        await structure.UpsertZonesAsync(newZones, cancellationToken);

        return await structure.GetZoneIdsAsync(stateId, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid, int), Guid>> EnsureSectionsAsync(
        TseStructureKeys keys,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        CancellationToken cancellationToken)
    {
        var sectionIds = await structure.GetSectionIdsAsync(stateId, cancellationToken);

        var newSections = keys.Sections()
            .Select(item => (
                item,
                MunicipalityId: municipalityIds.TryGetValue(item.Code, out var municipalityId)
                    ? municipalityId
                    : null,
                ZoneId: zoneIds.TryGetValue((stateId, item.Zone), out var zoneId) ? zoneId : (Guid?)null))
            .Where(item => item.MunicipalityId is not null
                           && item.ZoneId is not null
                           && !sectionIds.ContainsKey((
                               item.MunicipalityId.Value, item.ZoneId!.Value, item.item.Section)))

            // Deduplicate on the resolved key: two different TSE municipality codes can map to
            // the same IBGE municipality, which would otherwise emit the same conflict target
            // twice and make PostgreSQL reject the whole batch.

            .GroupBy(item => (item.MunicipalityId!.Value, item.ZoneId!.Value, item.item.Section))
            .Select(group => group.First())
            .Select(item => ElectoralSection.Create(
                item.MunicipalityId!.Value,
                item.ZoneId!.Value,
                item.item.Section))
            .ToArray();

        if (newSections.Length == 0)
            return sectionIds;

        await structure.UpsertSectionsAsync(newSections, cancellationToken);

        return await structure.GetSectionIdsAsync(stateId, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid?, int), Guid>> EnsurePollingPlacesAsync(
        TseStructureKeys keys,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        CancellationToken cancellationToken)
    {
        var placeIds = await structure.GetPollingPlaceIdsAsync(stateId, cancellationToken);

        var newPlaces = keys.PollingPlaces()
            .Select(item => (
                item,
                MunicipalityId: municipalityIds.TryGetValue(item.Code, out var municipalityId)
                    ? municipalityId
                    : null,
                ZoneId: zoneIds.TryGetValue((stateId, item.Zone), out var zoneId) ? zoneId : (Guid?)null))
            .Where(item => item.ZoneId is not null
                           && !placeIds.ContainsKey((stateId, item.ZoneId, item.item.Place)))
            .GroupBy(item => (item.ZoneId!.Value, item.item.Place))
            .Select(group => group.First())
            .Select(item =>
                PollingPlace.Create(
                    stateId,
                    item.MunicipalityId,
                    item.ZoneId,
                    item.item.Place,
                    $"Local {item.item.Place}",
                    null,
                    null,
                    null,
                    null,
                    null
                    )
            )
            .ToArray();

        if (newPlaces.Length == 0)
            return placeIds;

        await structure.UpsertPollingPlacesAsync(newPlaces, cancellationToken);

        return await structure.GetPollingPlaceIdsAsync(stateId, cancellationToken);
    }

    /// <summary>
    /// Assigns each section of the file to its polling place for every election round the file
    /// publishes. The compact key set carries the same information the full row set would.
    /// </summary>
    private async Task EnsureAssignmentsAsync(
        TseStructureKeys keys,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        IReadOnlyDictionary<(Guid, Guid, int), Guid> sectionIds,
        IReadOnlyDictionary<(Guid, Guid?, int), Guid> placeIds,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var roundIds = keys.Elections
            .Select(election => electionLookup.TryGetRoundId(
                election.ElectionCode, election.Round, out var roundId) ? roundId : (Guid?)null)
            .Where(roundId => roundId is not null)
            .Select(roundId => roundId!.Value)
            .Distinct()
            .ToArray();

        if (roundIds.Length == 0)
            return;

        var assignments = new Dictionary<(Guid RoundId, Guid SectionId), PollingPlaceSection>();

        foreach (var (code, zone, place) in keys.PollingPlaces())
        {
            if (!municipalityIds.TryGetValue(code, out var municipalityId)
                || municipalityId is null)
            {
                continue;
            }

            if (!zoneIds.TryGetValue((stateId, zone), out var zoneId))
                continue;

            if (!placeIds.TryGetValue((stateId, zoneId, place), out var placeId))
                continue;

            foreach (var section in keys.SectionsFor(code, zone))
            {
                if (!sectionIds.TryGetValue((municipalityId.Value, zoneId, section), out var sectionId))
                    continue;

                foreach (var roundId in roundIds)
                    assignments[(roundId, sectionId)] = PollingPlaceSection.Create(roundId, placeId, sectionId);
            }
        }

        if (assignments.Count > 0)
            await structure.UpsertAssignmentsAsync(assignments.Values.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Picks the municipality a new zone is associated with. TSE zones map to one municipality
    /// for the purposes of this table; taking the first resolved one preserves that intent
    /// without inventing a relationship.
    /// </summary>
    private static Guid? FirstMunicipalityInZone(
        TseStructureKeys keys,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        int zone)
    {
        foreach (var code in keys.MunicipalityCodes)
        {
            if (keys.SectionsFor(code, zone).Count > 0
                && municipalityIds.TryGetValue(code, out var municipalityId)
                && municipalityId is not null)
            {
                return municipalityId;
            }
        }

        return null;
    }
}
