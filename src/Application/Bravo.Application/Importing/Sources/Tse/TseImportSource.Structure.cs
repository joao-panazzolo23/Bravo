using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;

namespace Bravo.Application.Importing.Sources.Tse;

/// <summary>
/// Ensures the electoral structure a results file refers to (municipalities, zones, sections,
/// polling places and their round assignments) exists before votes are written.
/// </summary>
/// <remarks>
/// Structure is round-independent, so it is ensured once per (year, state) file. Every upsert is
/// keyed by a natural key, which is what makes re-running an import safe.
/// </remarks>
public sealed partial class TseImportSource
{
    private async Task EnsureStructureAsync(
        IReadOnlyList<TseSectionResultRow> rows,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        CancellationToken cancellationToken)
    {
        var resolved = rows
            .Where(row => municipalityIds.TryGetValue(row.TseMunicipalityCode, out var id) && id is not null)
            .Select(row => (Row: row, MunicipalityId: municipalityIds[row.TseMunicipalityCode]!.Value))
            .Distinct()
            .ToArray();

        if (resolved.Length == 0)
            return;

        var zoneIds = await EnsureZonesAsync(resolved, stateId, cancellationToken);
        var sectionIds = await EnsureSectionsAsync(resolved, stateId, zoneIds, cancellationToken);
        var placeIds = await EnsurePollingPlacesAsync(resolved, stateId, zoneIds, cancellationToken);

        await EnsureAssignmentsAsync(
            rows, stateId, municipalityIds, zoneIds, sectionIds, placeIds, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(Guid, int), Guid>> EnsureZonesAsync(
        (TseSectionResultRow Row, Guid MunicipalityId)[] resolved,
        Guid stateId,
        CancellationToken cancellationToken)
    {
        var zoneIds = await structure.GetZoneIdsAsync(cancellationToken);

        var newZones = resolved
            .GroupBy(item => item.Row.ZoneNumber)
            .Where(group => !zoneIds.ContainsKey((stateId, group.Key)))
            .Select(group => ElectoralZone.Create(
                stateId,
                group.Key,
                group.Select(item => (Guid?)item.MunicipalityId).FirstOrDefault()))
            .ToArray();

        if (newZones.Length == 0)
            return zoneIds;

        await structure.UpsertZonesAsync(newZones, cancellationToken);

        return await structure.GetZoneIdsAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid, int), Guid>> EnsureSectionsAsync(
        (TseSectionResultRow Row, Guid MunicipalityId)[] resolved,
        Guid stateId,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        CancellationToken cancellationToken)
    {
        var sectionIds = await structure.GetSectionIdsAsync(cancellationToken);

        var newSections = resolved
            .Select(item => (item.MunicipalityId, item.Row.ZoneNumber, item.Row.SectionNumber))
            .Distinct()
            .Select(key => (key, ZoneId: zoneIds.TryGetValue((stateId, key.ZoneNumber), out var zoneId)
                ? zoneId
                : (Guid?)null))
            .Where(item => item.ZoneId is not null
                           && !sectionIds.ContainsKey(
                               (item.key.MunicipalityId, item.ZoneId!.Value, item.key.SectionNumber)))
            .Select(item => ElectoralSection.Create(
                item.key.MunicipalityId, item.ZoneId!.Value, item.key.SectionNumber))
            .ToArray();

        if (newSections.Length == 0)
            return sectionIds;

        await structure.UpsertSectionsAsync(newSections, cancellationToken);

        return await structure.GetSectionIdsAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<(Guid, Guid?, int), Guid>> EnsurePollingPlacesAsync(
        (TseSectionResultRow Row, Guid MunicipalityId)[] resolved,
        Guid stateId,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        CancellationToken cancellationToken)
    {
        var placeIds = await structure.GetPollingPlaceIdsAsync(cancellationToken);

        var newPlaces = resolved
            .Select(item => (item.Row, item.MunicipalityId, item.Row.ZoneNumber, item.Row.PollingPlaceNumber))
            .Distinct()
            .Select(item => (item, ZoneId: zoneIds.TryGetValue((stateId, item.ZoneNumber), out var zoneId)
                ? zoneId
                : (Guid?)null))
            .Where(item => item.ZoneId is not null
                           && !placeIds.ContainsKey(
                               (stateId, item.ZoneId, item.item.PollingPlaceNumber)))
            .Select(item => PollingPlace.Create(
                stateId,
                item.item.MunicipalityId,
                item.ZoneId,
                item.item.PollingPlaceNumber,
                $"Local {item.item.PollingPlaceNumber}",
                null, null, null, null, null))
            .ToArray();

        if (newPlaces.Length == 0)
            return placeIds;

        await structure.UpsertPollingPlacesAsync(newPlaces, cancellationToken);

        return await structure.GetPollingPlaceIdsAsync(cancellationToken);
    }

    private async Task EnsureAssignmentsAsync(
        IReadOnlyList<TseSectionResultRow> rows,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        IReadOnlyDictionary<(Guid, Guid, int), Guid> sectionIds,
        IReadOnlyDictionary<(Guid, Guid?, int), Guid> placeIds,
        CancellationToken cancellationToken)
    {
        var assignments = new Dictionary<(Guid RoundId, Guid SectionId), PollingPlaceSection>();

        foreach (var roundGroup in rows.GroupBy(row => (row.ElectionCode, row.Round)))
        {
            var election = await elections.FindByCodeAsync(
                roundGroup.Key.ElectionCode, cancellationToken);

            if (election is null)
                continue;

            var round = await elections.FindRoundAsync(
                election.Id, roundGroup.Key.Round, cancellationToken);

            if (round is null)
                continue;

            foreach (var row in roundGroup)
            {
                var resolved = ResolveAssignmentKey(
                    row, stateId, municipalityIds, zoneIds, sectionIds, placeIds);

                if (resolved is not { } ids)
                    continue;

                assignments[(round.Id, ids.SectionId)] =
                    PollingPlaceSection.Create(round.Id, ids.PlaceId, ids.SectionId);
            }
        }

        if (assignments.Count > 0)
            await structure.UpsertAssignmentsAsync(assignments.Values.ToArray(), cancellationToken);
    }

    private static (Guid SectionId, Guid PlaceId)? ResolveAssignmentKey(
        TseSectionResultRow row,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        IReadOnlyDictionary<(Guid, int), Guid> zoneIds,
        IReadOnlyDictionary<(Guid, Guid, int), Guid> sectionIds,
        IReadOnlyDictionary<(Guid, Guid?, int), Guid> placeIds)
    {
        if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
            || municipalityId is null)
        {
            return null;
        }

        if (!zoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
            return null;

        if (!sectionIds.TryGetValue((municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
            return null;

        if (!placeIds.TryGetValue((stateId, zoneId, row.PollingPlaceNumber), out var placeId))
            return null;

        return (sectionId, placeId);
    }
}
