using System.Diagnostics;
using Bravo.Application.Importing.Services;
using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using Bravo.Application.Electoral;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;

namespace Bravo.Application.Importing.Sources.Tse;

public sealed partial class TseImportSource
{
    /// <summary>
    /// Imports section-level structure and voting results for every (year, state) pair.
    /// </summary>
    /// <remarks>
    /// Pairs are independent, so they run concurrently, but the number actually in flight is
    /// bounded by the process-wide resource governor rather than by this loop alone.
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

                // A result scope means one large CSV being streamed. The governor bounds how
                // many of those exist at once across every job in the process.
                using var permit = await resources.AcquireResultScopeAsync(token);

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

            return imported;
        }
        catch
        {
            await datasetVersions.FailAsync(version.Id, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Streams one results file twice: once to collect the compact geographic keys structure
    /// preparation needs, then again to transform and persist rows through a bounded pipeline.
    /// </summary>
    /// <remarks>
    /// The second read is sequential and costs a fraction of the file's parse time, and it is
    /// what lets the import keep memory proportional to the distinct geography of a state
    /// instead of to its 18 million result rows.
    /// </remarks>
    private async Task<int> ImportResultRowsAsync(
        int year,
        string state,
        AcquiredDataset dataset,
        DatasetVersion version,
        TseElectionLookup electionLookup,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var stateId = await ResolveResultStateIdAsync(state, cancellationToken);

        // Pass 1: geographic keys only.
        var keys = await CollectStructureKeysAsync(dataset, cancellationToken);

        if (keys.SectionCount == 0)
            return 0;

        var municipalityIds = await ResolveMunicipalitiesAsync(keys, stateId, year, cancellationToken);

        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);

        var structureIds = await EnsureStructureAsync(
            keys, stateId, municipalityIds, electionLookup, cancellationToken);

        var partyByNumber = partyIds
            .GroupBy(entry => entry.Key.Number)
            .ToDictionary(group => group.Key, group => group.First().Value);

        // Pass 2: stream rows through a bounded producer/consumer pipeline. The batch builder
        // allocates one buffer at a time and hands ownership to the channel, so memory never
        // scales with the file.
        var imported = await StreamResultsAsync(
            dataset,
            year,
            state,
            stateId,
            version.Id,
            keys,
            municipalityIds,
            structureIds,
            officeIds,
            partyByNumber,
            electionLookup,
            batchSize,
            cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(started);

        PersistenceLog.SectionResultsUpserted(logger, imported, year, state);

        ResourceLog.ResultScopeCompleted(
            logger,
            year,
            state,
            keys.SectionCount,
            elapsed.TotalMilliseconds,
            keys.SectionCount / Math.Max(elapsed.TotalSeconds, 0.001)
        );

        return imported;
    }

    private async Task<TseStructureKeys> CollectStructureKeysAsync(
        AcquiredDataset dataset,
        CancellationToken cancellationToken)
    {
        var keys = new TseStructureKeys();

        // A reader permit is held only while rows are being pulled, so the two passes of one
        // scope do not pin two reader slots permanently.
        using (await resources.AcquireCsvReaderAsync(cancellationToken))
        {
            await using var stream = File.OpenRead(dataset.FilePath);

            await foreach (var row in sectionReader.ReadAsync(
                               stream, dataset.LayoutVersion, cancellationToken))
            {
                // A non-positive location number is a source sentinel, not a place. Warn once
                // per scope so the data gap is visible without logging a line per row.
                if (row.PollingPlaceNumber <= 0 && !keys.SawSentinelPollingPlace)
                {
                    keys.SawSentinelPollingPlace = true;

                    ResourceLog.InvalidPollingPlaceSkipped(
                        logger, row.PollingPlaceNumber, row.Year, row.StateAcronym);
                }

                keys.Observe(row);
            }
        }

        return keys;
    }

    private async Task<int> StreamResultsAsync(
        AcquiredDataset dataset,
        int year,
        string state,
        Guid stateId,
        Guid versionId,
        TseStructureKeys keys,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        TseStructureIds structureIds,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<int, Guid> partyByNumber,
        TseElectionLookup electionLookup,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidateIds = await LoadCandidateIdsAsync(keys, stateId, electionLookup, cancellationToken);

        long imported = 0;

        await BoundedPipeline.RunAsync<ResultBatch>(
            resources.MaxInFlightBatchesPerScope,
            async (writer, token) =>
            {
                using var readerPermit = await resources.AcquireCsvReaderAsync(token);

                await using var stream = File.OpenRead(dataset.FilePath);

                var buffer = NewResultBuffer();
                var cursor = new ResolutionCursor(
                    stateId, municipalityIds, structureIds, officeIds, partyByNumber,
                    electionLookup, candidateIds);

                await foreach (var row in sectionReader.ReadAsync(stream, dataset.LayoutVersion, token))
                {
                    if (!cursor.TryResolve(row, out var key))
                        continue;

                    buffer[key] = buffer.TryGetValue(key, out var existing)
                        ? existing + row.Votes
                        : row.Votes;

                    if (buffer.Count < batchSize)
                        continue;

                    // Ownership of the buffer moves to the channel; a fresh one is allocated so
                    // the consumer never observes mutations.
                    await writer.WriteAsync(new ResultBatch(buffer), token);
                    buffer = NewResultBuffer();
                }

                if (buffer.Count > 0)
                    await writer.WriteAsync(new ResultBatch(buffer), token);
            },
            async (batch, token) =>
            {
                using var persistencePermit = await resources.AcquirePersistenceAsync(token);

                await FlushResultsAsync(batch, versionId, token);

                Interlocked.Add(ref imported, batch.Count);
            },
            cancellationToken);

        return (int)Interlocked.Read(ref imported);
    }

    /// <summary>
    /// Resolves rows to result keys using only preloaded maps. Held per pass, never shared, so
    /// it can be a plain mutable cursor without synchronisation.
    /// </summary>
    private readonly struct ResolutionCursor(
        Guid stateId,
        IReadOnlyDictionary<string, Guid?> municipalityIds,
        TseStructureIds structureIds,
        IReadOnlyDictionary<int, Guid> officeIds,
        IReadOnlyDictionary<int, Guid> partyByNumber,
        TseElectionLookup electionLookup,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, Guid>> candidateIds)
    {
        public bool TryResolve(
            TseSectionResultRow row,
            out (Guid RoundId, Guid SectionId, Guid OfficeId, VoteType Type, Guid? CandidateId, Guid? PartyId, int Ballot) key)
        {
            key = default;

            if (!municipalityIds.TryGetValue(row.TseMunicipalityCode, out var municipalityId)
                || municipalityId is null)
            {
                return false;
            }

            if (!structureIds.ZoneIds.TryGetValue((stateId, row.ZoneNumber), out var zoneId))
                return false;

            if (!structureIds.SectionIds.TryGetValue(
                    (municipalityId.Value, zoneId, row.SectionNumber), out var sectionId))
            {
                return false;
            }

            if (!officeIds.TryGetValue(row.OfficeCode, out var officeId))
                return false;

            if (!electionLookup.TryGetElectionId(row.ElectionCode, out var electionId))
                return false;

            if (!electionLookup.TryGetRoundId(row.ElectionCode, row.Round, out var roundId))
                return false;

            var voteType = TseVoteClassifier.Classify(row.BallotNumber, row.CandidateSequential);

            if (voteType == VoteType.Nominal)
            {
                if (candidateIds.TryGetValue(electionId, out var bySequential)
                    && bySequential.TryGetValue(row.CandidateSequential!, out var candidateId))
                {
                    key = (roundId, sectionId, officeId, voteType, candidateId, null, row.BallotNumber);
                    return true;
                }

                return false;
            }

            if (voteType == VoteType.Party)
            {
                if (partyByNumber.TryGetValue(row.BallotNumber, out var partyId))
                {
                    key = (roundId, sectionId, officeId, voteType, null, partyId, row.BallotNumber);
                    return true;
                }

                return false;
            }

            key = (roundId, sectionId, officeId, voteType, null, null, row.BallotNumber);
            return true;
        }
    }

    private sealed record ResultBatch(
        Dictionary<(Guid RoundId, Guid SectionId, Guid OfficeId, VoteType Type, Guid? CandidateId, Guid? PartyId, int Ballot), long> Values)
    {
        public int Count => Values.Count;
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

        return await syntheticScopes.EnsureAsync(state, cancellationToken);
    }

    /// <summary>
    /// Loads the candidate-sequential map for every election in the file that casts nominal
    /// votes, once each.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, Guid>>> LoadCandidateIdsAsync(
        TseStructureKeys keys,
        Guid stateId,
        TseElectionLookup electionLookup,
        CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<Guid, IReadOnlyDictionary<string, Guid>>();

        foreach (var (electionCode, _) in keys.Elections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!electionLookup.TryGetElectionId(electionCode, out var electionId))
                continue;

            if (loaded.ContainsKey(electionId))
                continue;

            loaded[electionId] = await candidates.GetCandidateIdsAsync(
                electionId, stateId, cancellationToken);
        }

        return loaded;
    }

    private static Dictionary<(Guid, Guid, Guid, VoteType, Guid?, Guid?, int), long> NewResultBuffer()
        => [];

    private async Task FlushResultsAsync(
        ResultBatch batch,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        var entities = batch.Values
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

        await results.UpsertResultsAsync(entities, cancellationToken);

        // Release the retained references as soon as the batch is durable.
        batch.Values.Clear();
    }

    private async Task<IReadOnlyDictionary<string, Guid?>> ResolveMunicipalitiesAsync(
        TseStructureKeys keys,
        Guid stateId,
        int year,
        CancellationToken cancellationToken)
    {
        var observations = keys.MunicipalityCodes
            .Select(code => (RawCode: code, MunicipalityName: keys.MunicipalityName(code), StateId: stateId))
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
