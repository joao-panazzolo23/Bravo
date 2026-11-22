using System.Diagnostics;
using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Tse.Models;
using Bravo.Application.Logging;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Geography.Repositories;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Imports section-level structure and voting results for every (year, state) pair.
/// </summary>
/// <remarks>
/// Pairs are independent, so they run concurrently, but the number actually in flight is bounded by
/// the process-wide resource governor rather than by this loop alone.
///
/// One scope is streamed twice. The first pass collects only the distinct geographic keys the
/// structure needs, which collapses an eighteen-million-row file into a few thousand entries; the
/// structure is then prepared once. The second pass streams the file again through a bounded
/// pipeline that transforms rows and persists batches. That is what keeps memory proportional to
/// the geography of a state instead of to its row count, and it is deliberately preserved.
///
/// Rows the source cannot resolve are counted, not silently dropped: they are reported as rejected
/// records so the job settles as <c>CompletedWithErrors</c> instead of claiming a clean import.
/// </remarks>
public sealed class TseResultImporter(
    ITseDatasetAcquirer acquirer,
    ITseSectionResultReader sectionReader,
    IGeographyRepository geography,
    IElectoralReferenceRepository reference,
    ICandidateRepository candidates,
    IElectionResultRepository results,
    ITseMunicipalityCodeRepository municipalityCodes,
    TseMunicipalityResolver municipalityResolver,
    TseScopeResolver scopeResolver,
    TseStructurePreparer structurePreparer,
    DatasetVersionService datasetVersions,
    TseImportSession session,
    ImportResourceGovernor resources,
    ILogger<TseResultImporter> logger)
{
    public async Task RunAsync(
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
        long rejected = 0;

        context.Report(ImportStage.Results, pairs.Length, completed, failed, processed, rejected, "Results");

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
                    var outcome = await ImportScopeAsync(
                        pair.Year, pair.State, electionLookup, context, token);

                    Interlocked.Add(ref processed, outcome.Imported);
                    Interlocked.Add(ref rejected, outcome.Rejected);
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
                    Volatile.Read(ref rejected),
                    $"Results {pair.Year}/{pair.State}");
            });
    }

    private async Task<TseScopeOutcome> ImportScopeAsync(
        int year,
        string state,
        TseElectionLookup electionLookup,
        ImportExecutionContext context,
        CancellationToken cancellationToken)
    {
        var dataset = await acquirer.AcquireResultsAsync(year, state, cancellationToken);
        session.TrackDirectory(Path.GetDirectoryName(dataset.FilePath));

        var version = await datasetVersions.RegisterAsync(
            TseImportSource.SourceKey, "votacao_secao", year, dataset,
            $"TSE:votacao_secao:{year}:{state}", context.JobId, cancellationToken);

        try
        {
            var outcome = await ImportRowsAsync(
                year, state, dataset, version, electionLookup, context.BatchSize, cancellationToken);

            await datasetVersions.CompleteAsync(version.Id, cancellationToken);

            return outcome;
        }
        catch
        {
            await datasetVersions.FailAsync(version.Id, CancellationToken.None);
            throw;
        }
    }

    private async Task<TseScopeOutcome> ImportRowsAsync(
        int year,
        string state,
        AcquiredDataset dataset,
        DatasetVersion version,
        TseElectionLookup electionLookup,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var stateIds = await geography.GetStateIdsByAcronymAsync(cancellationToken);
        var stateId = await scopeResolver.ResolveRequiredAsync(state, stateIds, cancellationToken);

        // Pass 1: geographic keys only.
        var keys = await CollectStructureKeysAsync(dataset, cancellationToken);

        if (keys.SectionCount == 0)
            return TseScopeOutcome.Empty;

        var municipalityIds = await ResolveMunicipalitiesAsync(keys, stateId, year, cancellationToken);

        var officeIds = await reference.GetOfficeIdsAsync(cancellationToken);
        var partyIds = await reference.GetPartyIdsAsync(cancellationToken);

        var structureIds = await structurePreparer.PrepareAsync(
            keys, stateId, municipalityIds, electionLookup, cancellationToken);

        var partyByNumber = partyIds
            .GroupBy(entry => entry.Key.Number)
            .ToDictionary(group => group.Key, group => group.First().Value);

        // Pass 2: stream rows through a bounded producer/consumer pipeline. The batch builder
        // allocates one buffer at a time and hands ownership to the channel, so memory never
        // scales with the file.
        var outcome = await StreamResultsAsync(
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

        PersistenceLog.SectionResultsUpserted(logger, (int)outcome.Imported, year, state);

        ResourceLog.ResultScopeCompleted(
            logger,
            year,
            state,
            keys.SectionCount,
            elapsed.TotalMilliseconds,
            keys.SectionCount / Math.Max(elapsed.TotalSeconds, 0.001));

        return outcome;
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

    private async Task<TseScopeOutcome> StreamResultsAsync(
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

        var resolver = new TseResultRowResolver(
            stateId, municipalityIds, structureIds, officeIds, partyByNumber, electionLookup, candidateIds);

        long imported = 0;
        long rejected = 0;

        await BoundedPipeline.RunAsync<TseResultBatch>(
            resources.MaxInFlightBatchesPerScope,
            async (writer, token) =>
            {
                using var readerPermit = await resources.AcquireCsvReaderAsync(token);

                await using var stream = File.OpenRead(dataset.FilePath);

                var accumulator = new TseResultAccumulator(batchSize);

                await foreach (var row in sectionReader.ReadAsync(stream, dataset.LayoutVersion, token))
                {
                    if (!resolver.TryResolve(row, out var key, out var rejection))
                    {
                        Interlocked.Increment(ref rejected);

                        TseImportLog.ResultRowRejected(logger, year, state, rejection);
                        continue;
                    }

                    if (!accumulator.Add(key, row.Votes))
                        continue;

                    // Ownership of the buffer moves to the channel; a fresh accumulator is
                    // allocated so the consumer never observes mutations.
                    await writer.WriteAsync(new TseResultBatch(accumulator.Take()), token);
                }

                if (accumulator.Count > 0)
                    await writer.WriteAsync(new TseResultBatch(accumulator.Take()), token);
            },
            async (batch, token) =>
            {
                using var persistencePermit = await resources.AcquirePersistenceAsync(token);

                await FlushAsync(batch, versionId, token);

                Interlocked.Add(ref imported, batch.Count);
            },
            cancellationToken);

        return new TseScopeOutcome(Interlocked.Read(ref imported), Interlocked.Read(ref rejected));
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

    private async Task FlushAsync(
        TseResultBatch batch,
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

    private static ParallelOptions ParallelOptionsFor(
        ImportExecutionContext context,
        CancellationToken cancellationToken) => new()
        {
            MaxDegreeOfParallelism = Math.Max(1, context.MaxParallelism),
            CancellationToken = cancellationToken
        };
}

/// <summary>
/// Counts for one (year, state) scope: how many rows were persisted and how many the source could
/// not resolve.
/// </summary>
public readonly record struct TseScopeOutcome(long Imported, long Rejected)
{
    public static TseScopeOutcome Empty => new(0, 0);
}

/// <summary>
/// One batch of accumulated results handed from the parser to persistence.
/// </summary>
public sealed record TseResultBatch(Dictionary<TseResultKey, long> Values)
{
    public int Count => Values.Count;
}
