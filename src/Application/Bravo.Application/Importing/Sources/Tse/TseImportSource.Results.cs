using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using Bravo.Application.Electoral;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Sources.Tse;

public sealed partial class TseImportSource
{
    /// <summary>
    /// Imports section-level structure and voting results for every (year, state) pair.
    /// </summary>
    /// <remarks>
    /// Pairs are independent, so they run concurrently. Every stored result carries the active
    /// dataset version, which is what keeps a corrected archive from double-counting votes when
    /// the whole import is re-executed.
    /// </remarks>
    private async Task ImportResultsAsync(
        ImportExecutionContext context,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var pairs = TseCatalog.ResultYears
            .SelectMany(year => TseCatalog.ResultStates.Select(state => (Year: year, State: state)))
            .ToArray();

        var completed = 0;
        var failed = 0;
        long processed = 0;

        context.Report(
            ImportStage.Results,
            pairs.Length,
            completed,
            failed,
            processed,
            0,
            "Results");

        await Parallel.ForEachAsync(
            pairs,
            ParallelOptionsFor(context, cancellationToken),
            async (pair, token) =>
            {
                using var scope = logger.BeginOperationScope(year: pair.Year, state: pair.State);

                try
                {
                    var count = await ImportResultScopeAsync(
                        pair.Year, pair.State, electionLookup, context, token);

                    Interlocked.Add(ref processed, count);
                    Interlocked.Increment(ref completed);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failed);

                    ProcessingLog.ResultScopeImportFailed(logger, pair.Year, pair.State, exception);
                }

                context.Report(
                    ImportStage.Results,
                    pairs.Length,
                    Volatile.Read(ref completed),
                    Volatile.Read(ref failed),
                    Volatile.Read(ref processed),
                    0,
                    $"Results {pair.Year}/{pair.State}");
            });
    }

    private async Task<int> ImportResultScopeAsync(
        int year,
        string state,
        TseElectionLookup electionLookup,
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var dataset = await acquirer.AcquireResultsAsync(year, state, cancellationToken);

        var version = await datasetVersions.RegisterAsync(
            SourceKey, "votacao_secao", year, dataset,
            $"TSE:votacao_secao:{year}:{state}", context.JobId, cancellationToken);

        try
        {
            var imported = await ImportResultRowsAsync(
                year, state, dataset, version, electionLookup, context.BatchSize, cancellationToken);

            await datasetVersions.CompleteAsync(version.Id, cancellationToken);

            PersistenceLog.SectionResultsUpserted(
                logger, imported, year, state);

            return imported;
        }
        catch
        {
            await datasetVersions.FailAsync(version.Id, CancellationToken.None);
            throw;
        }
    }

    private async Task<int> ImportResultRowsAsync(
        int year,
        string state,
        AcquiredDataset dataset,
        DatasetVersion version,
        TseElectionLookup electionLookup,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(dataset.FilePath);
        var rows = sectionReader.Read(stream, dataset.LayoutVersion);

        if (rows.Count == 0)
            return 0;

        var stateId = await ResolveResultStateIdAsync(state, cancellationToken);
        var municipalityIds = await ResolveMunicipalitiesAsync(rows, stateId, year, cancellationToken);

        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);

        // Structure is round-independent, so it is ensured once per file. The scoped reads are
        // shared between ensuring structure and resolving rows, so each map is fetched once.
        var structureIds = await EnsureStructureAsync(
            rows, stateId, municipalityIds, electionLookup, cancellationToken);

        var partyByNumber = partyIds
            .GroupBy(entry => entry.Key.Number)
            .ToDictionary(group => group.Key, group => group.First().Value);

        // Candidate maps are fetched once per (election, state) file before the row loop, so
        // no database access happens while resolving individual records.
        var candidateCache = await LoadCandidateIdsAsync(
            rows, stateId, electionLookup, cancellationToken);

        var buffer = NewResultBuffer();

        var imported = 0;

        foreach (var row in rows)
        {
            var resolved = ResolveResultRow(
                row, stateId, municipalityIds, structureIds, officeIds,
                partyByNumber, electionLookup, candidateCache);

            if (resolved is not { } key)
                continue;

            buffer[key] = buffer.TryGetValue(key, out var existing)
                ? existing + row.Votes
                : row.Votes;

            if (buffer.Count < batchSize)
                continue;

            imported += await FlushResultsAsync(buffer, version.Id, cancellationToken);
        }

        imported += await FlushResultsAsync(buffer, version.Id, cancellationToken);

        return imported;
    }

    private async Task<Guid> ResolveResultStateIdAsync(
        string state,
        CancellationToken cancellationToken)
    {
        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);

        if (stateIds.TryGetValue(state, out var stateId))
            return stateId;

        if (!TseScopes.IsSynthetic(state))
        {
            throw new InvalidOperationException(
                $"TSE state '{state}' is not present in the geography import.");
        }

        return await syntheticScopes.EnsureAsync(
            state,
            geography.UpsertStatesAsync,
            cancellationToken);
    }

    /// <summary>
    /// Loads the candidate-sequential map for every election in the file that casts nominal
    /// votes, once each.
    /// </summary>
    /// <remarks>
    /// Nominal results need candidate identifiers, and a file spans a bounded set of elections.
    /// Fetching them up front keeps record resolution free of database access while issuing at
    /// most one query per distinct election with nominal votes.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, Guid>>> LoadCandidateIdsAsync(
        IReadOnlyList<TseSectionResultRow> rows,
        Guid stateId,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var electionCodes = rows
            .Where(row => TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential)
                          == VoteType.Nominal)
            .Select(row => row.ElectionCode)
            .Distinct();

        var loaded = new Dictionary<Guid, IReadOnlyDictionary<string, Guid>>();

        foreach (var code in electionCodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!electionLookup.TryGetElectionId(code, out var electionId))
                continue;

            loaded[electionId] = await candidates.GetCandidateIdsAsync(
                electionId, stateId, cancellationToken);
        }

        return loaded;
    }

    /// <summary>
    /// Maps one source row to its result key, or null when a reference is genuinely missing.
    /// </summary>
    /// <remarks>
    /// The caller accumulates votes per key, so duplicates inside one file are summed rather
    /// than overwritten. Every reference comes from a map loaded before the loop, so this
    /// method never touches the database.
    /// </remarks>
    private (Guid RoundId, Guid SectionId, Guid OfficeId, VoteType Type, Guid? CandidateId, Guid? PartyId, int Ballot)? ResolveResultRow(
        TseSectionResultRow row,
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        TseStructureIds structureIds,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<int, Guid> partyByNumber,
        TseElectionLookup electionLookup,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, Guid>> candidateIds)
    {
        if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
            || municipalityId is null)
        {
            return null;
        }

        if (!structureIds.ZoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
            return null;

        if (!structureIds.SectionIds.TryGetValue(
                (municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
        {
            return null;
        }

        if (!officeIds.TryGetValue(row.OfficeCode, out var officeId))
            return null;

        if (!electionLookup.TryGetElectionId(row.ElectionCode, out var electionId))
            return null;

        if (!electionLookup.TryGetRoundId(row.ElectionCode, row.Round, out var roundId))
            return null;

        var voteType = TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential);

        if (voteType == VoteType.Nominal)
        {
            return candidateIds.TryGetValue(electionId, out var bySequential)
                   && bySequential.TryGetValue(row.CandidateSequential!, out var candidateId)
                ? (roundId, sectionId, officeId, voteType, candidateId, null, row.BallotNumber)
                : null;
        }

        if (voteType == VoteType.Party)
        {
            return partyByNumber.TryGetValue(row.BallotNumber, out var partyId)
                ? (roundId, sectionId, officeId, voteType, null, partyId, row.BallotNumber)
                : null;
        }

        return (roundId, sectionId, officeId, voteType, null, null, row.BallotNumber);
    }

    private static Dictionary<(Guid, Guid, Guid, VoteType, Guid?, Guid?, int), long> NewResultBuffer()
        => [];

    private async Task<int> FlushResultsAsync(
        Dictionary<(Guid RoundId, Guid SectionId, Guid OfficeId, VoteType Type, Guid? CandidateId, Guid? PartyId, int Ballot), long> buffer,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (buffer.Count == 0)
            return 0;

        var batch = buffer
            .Select(entry => ElectionResult.Create(
                entry.Key.RoundId,
                entry.Key.SectionId,
                entry.Key.OfficeId,
                entry.Key.Type,
                entry.Key.CandidateId,
                entry.Key.PartyId,
                entry.Key.Ballot,
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

        var resolved = await municipalityResolver.ResolveAsync(
            observations, stateId, year, cancellationToken);

        await municipalityCodes.UpsertAsync(resolved, cancellationToken);

        return resolved.ToDictionary(
            code => code.RawCode,
            code => code.MunicipalityId,
            StringComparer.Ordinal);
    }
}
