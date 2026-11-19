using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Models;
using Bravo.Application.Importing;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Electoral.Services;


//todo: refactor. trash code.
/// <summary>
/// Imports section-level electoral structure and voting results for one (year, state).
/// </summary>
/// <remarks>
/// A single file can contain more than one round, distinguished by <c>CD_ELEICAO</c>, so the
/// round is resolved per row rather than per file. Every stored result carries the active
/// <c>DatasetVersionId</c>, so a corrected source file produces a new version instead of
/// duplicating votes.
/// </remarks>
public sealed class TseResultsPipeline(
    ITseDatasetAcquirer acquirer,
    ITseSectionResultReader reader,
    DatasetVersionService datasetVersions,
    IElectionRepository elections,
    IElectoralReferenceRepository reference,
    IElectoralStructureRepository structure,
    IGeographyRepository geography,
    ITseMunicipalityCodeRepository municipalityCodes,
    TseMunicipalityResolver municipalityResolver,
    ICandidateRepository candidates,
    IElectionResultRepository results,
    TseSyntheticScopeSeeder syntheticScopes,
    ImportProgressReporterFactory progressFactory,
    ILogger<TseResultsPipeline> logger) : IImportPipeline
{
    private const int BatchSize = 5_000;

    public string UnitKey => TseImport.ResultsStage;

    public async Task ProcessAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken = default
    )
    {
        if (!TseImport.TryParseResults(workUnit.UnitKey, out var year, out var state))
            throw new ImportPermanentException(TseImport.DescribeInvalidKey(workUnit.UnitKey));

        using var unitScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Dataset"] = TseImport.ResultsStage,
            ["ElectionYear"] = year,
            ["State"] = state
        });

        var progress = progressFactory.Create(job, workUnit);

        await progress.StageAsync(ImportStage.Downloading, cancellationToken);
        logger.LogInformation(
            "Downloading results dataset. Dataset: {Dataset}, ElectionYear: {Year}, State: {State}.",
            TseImport.ResultsStage, year, state);

        var dataset = await acquirer.AcquireResultsAsync(year, state, cancellationToken);

        await progress.StageAsync(ImportStage.Extracting, cancellationToken);

        var version = await datasetVersions.RegisterAsync(
            "TSE", "votacao_secao", year, dataset,
            $"TSE:votacao_secao:{year}:{state}", job.Id, cancellationToken);

        try
        {
            await ImportAsync(year, state, dataset, version, progress, cancellationToken);
            await progress.FlushAsync(cancellationToken);
            await datasetVersions.CompleteAsync(version.Id, cancellationToken);
        }
        catch
        {
            await datasetVersions.FailAsync(version.Id, CancellationToken.None);
            throw;
        }
    }

    private async Task ImportAsync(
        int year,
        string state,
        AcquiredDataset dataset,
        DatasetVersion version,
        IImportProgressReporter progress,
        CancellationToken cancellationToken)
    {
        await progress.StageAsync(ImportStage.Reading, cancellationToken);

        await using var stream = File.OpenRead(dataset.FilePath);

        var rows = reader.Read(stream, dataset.LayoutVersion);

        if (rows.Count == 0)
            return;

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (!stateIds.TryGetValue(state, out var stateId))
        {
            if (!TseScopes.IsSynthetic(state))
            {
                throw new ImportPermanentException(
                    $"State '{state}' is not present in the geography import.");
            }

            stateId = await syntheticScopes.EnsureAsync(
                state,
                (states, token) => geography.UpsertStatesAsync(states, token),
                cancellationToken);
        }

        var municipalityIds = await ResolveMunicipalitiesAsync(rows, stateId, year, cancellationToken);

        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);
        var zoneIds = await structure.GetZoneIdsAsync(cancellationToken);
        var sectionIds = await structure.GetSectionIdsAsync(cancellationToken);

        var rounds = new Dictionary<int, Guid>();
        var candidateCache = new Dictionary<Guid, IReadOnlyDictionary<string, Guid>>();

        // Structure is round-independent, so it is ensured once per file.
        await EnsureStructureAsync(rows, stateId, municipalityIds, cancellationToken);

        zoneIds = await structure.GetZoneIdsAsync(cancellationToken);
        sectionIds = await structure.GetSectionIdsAsync(cancellationToken);

        var partyByNumber = partyIds
            .GroupBy(entry => entry.Key.Number)
            .ToDictionary(group => group.Key, group => group.First().Value);

        var buffer = new Dictionary<(Guid RoundId, Guid SectionId, Guid OfficeId, VoteType Type, Guid? CandidateId, Guid? PartyId, int Ballot), long>(BatchSize);
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

            var election = await elections.FindByCodeAsync(row.ElectionCode, cancellationToken);

            if (election is null)
                continue;

            if (!rounds.TryGetValue(row.ElectionCode, out var roundId))
            {
                var round = await elections.FindRoundAsync(election.Id, row.Round, cancellationToken);

                if (round is null)
                    continue;

                roundId = round.Id;
                rounds[row.ElectionCode] = roundId;
            }

            if (!candidateCache.TryGetValue(election.Id, out var candidateIds))
            {
                candidateIds = await candidates.GetCandidateIdsAsync(election.Id, stateId, cancellationToken);
                candidateCache[election.Id] = candidateIds;
            }

            var voteType = TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential);
            Guid? candidateId = null;
            Guid? partyId = null;

            if (voteType == VoteType.Nominal)
            {
                if (!candidateIds.TryGetValue(row.CandidateSequential!, out var resolved))
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

            var key = (roundId, sectionId, officeId, voteType, candidateId, partyId, row.BallotNumber);

            buffer[key] = buffer.TryGetValue(key, out var existing) ? existing + row.Votes : row.Votes;

            if (buffer.Count >= BatchSize)
            {
                await progress.StageAsync(ImportStage.Persisting, cancellationToken);
                imported += await FlushAsync(buffer, version.Id, cancellationToken);

                await progress.RecordsAsync(
                    imported, imported, 0, 0,
                    cancellationToken: cancellationToken);
            }
        }

        imported += await FlushAsync(buffer, version.Id, cancellationToken);

        await progress.RecordsAsync(imported, imported, 0, 0, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Imported {Imported} section results for {Year}/{State}; {Unresolved} unresolved, {UnknownCandidate} unknown candidate, {Rounds} rounds.",
            imported, year, state, unresolved, unknownCandidate, rounds.Count);
    }

    private async Task<int> FlushAsync(
        Dictionary<(Guid, Guid, Guid, VoteType, Guid?, Guid?, int), long> buffer,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (buffer.Count == 0)
            return 0;

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

        var count = buffer.Count;
        buffer.Clear();

        return count;
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

        return resolved.ToDictionary(code => code.RawCode, code => code.MunicipalityId, StringComparer.Ordinal);
    }

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

        var zoneIds = await structure.GetZoneIdsAsync(cancellationToken);

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
            .Select(key => (key, ZoneId: zoneIds.TryGetValue((stateId, key.ZoneNumber), out var zoneId) ? zoneId : (Guid?)null))
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
            .Select(item => (item.Row, item.MunicipalityId, item.Row.ZoneNumber, item.Row.PollingPlaceNumber))
            .Distinct()
            .Select(item => (item, ZoneId: zoneIds.TryGetValue((stateId, item.ZoneNumber), out var zoneId) ? zoneId : (Guid?)null))
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

        var roundsByElection = rows
            .GroupBy(row => (row.ElectionCode, row.Round))
            .ToArray();

        var assignments = new Dictionary<(Guid RoundId, Guid SectionId), PollingPlaceSection>();

        foreach (var roundGroup in roundsByElection)
        {
            var election = await elections.FindByCodeAsync(roundGroup.Key.ElectionCode, cancellationToken);

            if (election is null)
                continue;

            var round = await elections.FindRoundAsync(election.Id, roundGroup.Key.Round, cancellationToken);

            if (round is null)
                continue;

            foreach (var row in roundGroup)
            {
                if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
                    || municipalityId is null)
                {
                    continue;
                }

                if (!zoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
                    continue;

                if (!sectionIds.TryGetValue((municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
                    continue;

                if (!placeIds.TryGetValue((stateId, zoneId, row.PollingPlaceNumber), out var placeId))
                    continue;

                assignments[(round.Id, sectionId)] =
                    PollingPlaceSection.Create(round.Id, placeId, sectionId);
            }
        }

        if (assignments.Count > 0)
            await structure.UpsertAssignmentsAsync(assignments.Values.ToArray(), cancellationToken);
    }
}
