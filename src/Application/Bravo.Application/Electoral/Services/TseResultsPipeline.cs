using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Models;
using Bravo.Application.Importing;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Electoral.Services;

/// <summary>
/// Imports section-level electoral structure and voting results for one TSE scope.
/// </summary>
/// <remarks>
/// Every stored result carries the active <c>DatasetVersionId</c>, so a corrected source file
/// produces a new version rather than duplicating votes. Rows whose municipality or candidate
/// cannot be resolved are counted and excluded, never attributed to a guessed entity.
/// </remarks>
public sealed class TseResultsPipeline(
    ITseDatasetAcquirer acquirer,
    ITseSectionResultReader reader,
    DatasetVersionService datasetVersions,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    IElectoralStructureRepository structure,
    IGeographyLookup geography,
    ITseMunicipalityCodeRepository municipalityCodes,
    TseMunicipalityResolver municipalityResolver,
    ICandidateRepository candidates,
    IElectionResultRepository results,
    ILogger<TseResultsPipeline> logger) : IImportPipeline
{
    private const int BatchSize = 5_000;

    public string UnitKey => TseImport.ResultsStage;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default)
    {
        var scope = TseImportScope.Parse(workUnit.UnitKey);

        var dataset = await acquirer.AcquireResultsAsync(scope, cancellationToken);

        var version = await datasetVersions.RegisterAsync(
            "TSE", "votacao_secao", scope.Year, dataset, ScopeName(scope), job.Id, cancellationToken);

        try
        {
            await ImportAsync(scope, dataset, version, cancellationToken);
            await datasetVersions.CompleteAsync(version.Id, cancellationToken);
        }
        catch
        {
            await datasetVersions.FailAsync(version.Id, CancellationToken.None);
            throw;
        }
    }

    private static string ScopeName(TseImportScope scope)
        => $"TSE:votacao_secao:{scope.Year}:{scope.State}:round-{scope.Round}";

    private async Task ImportAsync(
        TseImportScope scope,
        AcquiredDataset dataset,
        DatasetVersion version,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(dataset.FilePath);

        var rows = reader.Read(stream, dataset.LayoutVersion);

        if (rows.Count == 0)
            return;

        var election = await elections.FindByCodeAsync(scope.ElectionCode, cancellationToken)
            ?? throw new ImportPermanentException(
                $"Election {scope.ElectionCode} is not imported; run the reference stage first.");

        var round = await elections.FindRoundAsync(election.Id, scope.Round, cancellationToken)
            ?? throw new ImportPermanentException(
                $"Round {scope.Round} is not imported; run the reference stage first.");

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (!stateIds.TryGetValue(scope.State, out var stateId))
            throw new ImportPermanentException($"State '{scope.State}' is not imported.");

        var municipalityIds = await ResolveMunicipalitiesAsync(rows, stateId, scope.Year, cancellationToken);

        await EnsureStructureAsync(rows, round, stateId, municipalityIds, cancellationToken);

        var zoneIds = await structure.GetZoneIdsAsync(cancellationToken);
        var sectionIds = await structure.GetSectionIdsAsync(cancellationToken);
        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var candidateIds = await candidates.GetCandidateIdsAsync(election.Id, cancellationToken);

        var partyByNumber = partyIds
            .GroupBy(entry => entry.Key.Number)
            .ToDictionary(group => group.Key, group => group.First().Value);

        var buffer = new Dictionary<(Guid, Guid, Guid, VoteType, Guid?, Guid?, int), long>(BatchSize);
        var imported = 0;
        var unresolved = 0;
        var unknownCandidate = 0;

        foreach (var row in rows)
        {
            if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
                || municipalityId is null)
            {
                unresolved++;
                continue;
            }

            if (!zoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
            {
                unresolved++;
                continue;
            }

            if (!sectionIds.TryGetValue((municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
            {
                unresolved++;
                continue;
            }

            if (!officeIds.TryGetValue(row.OfficeCode, out var officeId))
                continue;

            var voteType = TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential);
            Guid? candidateId = null;
            Guid? partyId = null;

            if (voteType == VoteType.Nominal)
            {
                if (!candidateIds.TryGetValue(row.CandidateSequential!.Value, out var resolved))
                {
                    unknownCandidate++;
                    continue;
                }

                candidateId = resolved;
            }
            else if (voteType == VoteType.Party)
            {
                if (!partyByNumber.TryGetValue(row.BallotNumber, out var resolvedParty))
                    continue;

                partyId = resolvedParty;
            }

            var key = (
                round.Id, sectionId, officeId, voteType, candidateId, partyId, row.BallotNumber);

            if (buffer.TryGetValue(key, out var existing))
            {
                buffer[key] = existing + row.Votes;
            }
            else
            {
                buffer[key] = row.Votes;
            }

            imported++;

            if (buffer.Count >= BatchSize)
            {
                await FlushAsync(buffer, round.Id, version.Id, cancellationToken);
            }
        }

        await FlushAsync(buffer, round.Id, version.Id, cancellationToken);

        logger.LogInformation(
            "Imported {Imported} section results for {Year}/{State} round {Round}; {Unresolved} unresolved, {UnknownCandidate} unknown candidate.",
            imported, scope.Year, scope.State, scope.Round, unresolved, unknownCandidate);
    }

    private async Task FlushAsync(
        Dictionary<(Guid, Guid, Guid, VoteType, Guid?, Guid?, int), long> buffer,
        Guid roundId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (buffer.Count == 0)
            return;

        var batch = buffer
            .Select(entry => ElectionResult.Create(
                entry.Key.Item1,
                entry.Key.Item2,
                entry.Key.Item3,
                entry.Key.Item4,
                entry.Key.Item5,
                entry.Key.Item6,
                entry.Key.Item7,
                entry.Value,
                versionId))
            .ToArray();

        await results.UpsertResultsAsync(batch, cancellationToken);

        buffer.Clear();
    }

    private async Task<IReadOnlyDictionary<string, Guid?>> ResolveMunicipalitiesAsync(
        IReadOnlyList<TseSectionResultRow> rows,
        Guid stateId,
        int year,
        CancellationToken cancellationToken)
    {
        var observations = rows
            .Select(row => (row.TseMunicipalityCode, row.MunicipalityName, stateId))
            .Distinct()
            .ToArray();

        var resolved = await municipalityResolver.ResolveAsync(observations, year, cancellationToken);

        await municipalityCodes.UpsertAsync(resolved, cancellationToken);

        return resolved.ToDictionary(
            code => code.RawCode,
            code => code.MunicipalityId,
            StringComparer.Ordinal);
    }

    private async Task EnsureStructureAsync(
        IReadOnlyList<TseSectionResultRow> rows,
        ElectionRound round,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        CancellationToken cancellationToken)
    {
        var resolved = rows
            .Where(row => municipalityIds.TryGetValue(row.TseMunicipalityCode, out var id) && id is not null)
            .Select(row => (Row: row, MunicipalityId: municipalityIds[row.TseMunicipalityCode]!.Value))
            .ToArray();

        var zoneIds = await structure.GetZoneIdsAsync(cancellationToken);

        // Zones are unique per (StateId, TseZoneNumber); a zone can span municipalities, so
        // dedupe on the actual constraint key and keep one representative municipality.
        var newZones = resolved
            .GroupBy(item => item.Row.ZoneNumber)
            .Where(group => !zoneIds.ContainsKey((stateId, group.Key)))
            .Select(group => ElectoralZone.Create(
                stateId,
                group.Key,
                group.Select(item => (Guid?)item.MunicipalityId).FirstOrDefault()))
            .ToArray();

        if (newZones.Length > 0)
        {
            await structure.UpsertZonesAsync(newZones, cancellationToken);
            zoneIds = await structure.GetZoneIdsAsync(cancellationToken);
        }

        var sectionIds = await structure.GetSectionIdsAsync(cancellationToken);

        var newSections = resolved
            .Select(item => (item.MunicipalityId, item.Row.ZoneNumber, item.Row.SectionNumber))
            .Distinct()
            .Select(key => (
                key,
                ZoneId: zoneIds.TryGetValue((stateId, key.ZoneNumber), out var zoneId) ? zoneId : (Guid?)null))
            .Where(item => item.ZoneId is not null
                           && !sectionIds.ContainsKey((item.key.MunicipalityId, item.ZoneId!.Value, item.key.SectionNumber)))
            .Select(item => ElectoralSection.Create(item.key.MunicipalityId, item.ZoneId!.Value, item.key.SectionNumber))
            .ToArray();

        if (newSections.Length > 0)
        {
            await structure.UpsertSectionsAsync(newSections, cancellationToken);
            sectionIds = await structure.GetSectionIdsAsync(cancellationToken);
        }

        var placeIds = await structure.GetPollingPlaceIdsAsync(cancellationToken);

        var newPlaces = resolved
            .GroupBy(item => item.Row.ZoneNumber)
            .SelectMany(group => group
                .GroupBy(item => item.Row.PollingPlaceNumber)
                .Select(placeGroup => (
                    Row: placeGroup.First().Row,
                    MunicipalityId: placeGroup.First().MunicipalityId,
                    ZoneNumber: group.Key,
                    PollingPlaceNumber: placeGroup.Key)))
            .Select(item => (
                item,
                ZoneId: zoneIds.TryGetValue((stateId, item.ZoneNumber), out var zoneId) ? zoneId : (Guid?)null))
            .Where(item => item.ZoneId is not null
                           && !placeIds.ContainsKey((stateId, item.ZoneId, item.item.PollingPlaceNumber)))
            .Select(item => PollingPlace.Create(
                stateId,
                item.item.MunicipalityId,
                item.ZoneId,
                item.item.PollingPlaceNumber,
                $"Local {item.item.PollingPlaceNumber}",
                null, null, null, null, null))
            .ToArray();

        if (newPlaces.Length > 0)
        {
            await structure.UpsertPollingPlacesAsync(newPlaces, cancellationToken);
            placeIds = await structure.GetPollingPlaceIdsAsync(cancellationToken);
        }

        var assignments = new Dictionary<Guid, PollingPlaceSection>(sectionIds.Count);

        foreach (var item in resolved)
        {
            if (!zoneIds.TryGetValue((stateId, item.Row.ZoneNumber), out var zoneId))
                continue;

            if (!sectionIds.TryGetValue((item.MunicipalityId, zoneId, item.Row.SectionNumber), out var sectionId))
                continue;

            if (assignments.ContainsKey(sectionId))
                continue;

            if (!placeIds.TryGetValue((stateId, zoneId, item.Row.PollingPlaceNumber), out var placeId))
                continue;

            assignments[sectionId] = PollingPlaceSection.Create(round.Id, placeId, sectionId);
        }

        if (assignments.Count > 0)
            await structure.UpsertAssignmentsAsync(assignments.Values.ToArray(), cancellationToken);
    }
}
