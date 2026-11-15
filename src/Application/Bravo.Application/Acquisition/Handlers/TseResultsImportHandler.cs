using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Acquisition.Handlers;

/// <summary>
/// Imports TSE polling-section results and turnout statistics for one election and round.
///
/// Two official datasets are acquired: the candidate vote file and the polling-section detail
/// file. Sections, zones and polling places are derived from the vote file before the facts are
/// merged, so results can reference their geography. Both files are streamed in bounded batches
/// with PostgreSQL COPY staging and one explicit transaction per batch.
///
/// The election identity is read from the source rows. When a state is given in the scope, the
/// per-state archive is used; the national archive is only used for national scopes.
/// </summary>
public class TseResultsImportHandler(
    ImportExecutionRunner runner,
    IDatasetProvenanceRepository provenanceRepository,
    ITseProvider provider,
    IElectoralResultsPersistenceService resultsPersistence,
    IElectoralPersistenceService electoralPersistence,
    ITemporaryFileStore fileStore,
    IUnityOfWork unityOfWork,
    IOptions<AcquisitionOptions> options,
    ILogger<TseResultsImportHandler> logger)
{
    private const string ResultsKey = "tse-section-results";
    private const string StatisticsKey = "tse-section-statistics";
    private const string SectionsKey = "tse-sections";

    /// <summary>
    /// Elections and contests already ensured during this execution. Metadata handling is
    /// idempotent but repeating it per batch would multiply the round trips.
    /// </summary>
    private readonly Dictionary<string, Guid> _elections = new(StringComparer.Ordinal);
    private readonly HashSet<string> _contests = new(StringComparer.Ordinal);

    public async Task ExecuteAsync(Guid executionId, ImportScope scope, CancellationToken cancellationToken)
    {
        if (scope.Year is not { } year)
        {
            logger.LogError("TSE results import requires an election year.");
            return;
        }

        var execution = await runner.OpenAsync(executionId, cancellationToken);
        if (execution is null)
        {
            return;
        }

        var counters = new Counters();
        var files = new List<string>();

        try
        {
            var completed = await runner.GetCompletedKeysAsync(execution, cancellationToken);

            await runner.SetStageAsync(execution, "ResolvingDatasets", cancellationToken);
            var resultsDataset = await provider.ResolveDataset(
                TseDatasetKind.PollingSectionResults, year, scope.State, cancellationToken);
            var detailsDataset = await provider.ResolveDataset(
                TseDatasetKind.PollingSectionDetails, year, scope.State, cancellationToken);

            // The vote file is processed first: it creates the section geography the statistics
            // and the facts depend on.
            var resultsFile = await DownloadAsync(execution, resultsDataset, counters, cancellationToken);
            files.Add(resultsFile.FilePath);

            if (!completed.Contains(SectionsKey))
            {
                await runner.SetStageAsync(execution, "StreamingSections", cancellationToken);
                await ImportSectionsAsync(execution, resultsDataset, resultsFile, scope.State, counters, cancellationToken);
            }

            if (!completed.Contains(ResultsKey))
            {
                await runner.SetStageAsync(execution, "StreamingResults", cancellationToken);
                await ImportResultsAsync(execution, resultsDataset, resultsFile, scope.State, counters, cancellationToken);
            }

            if (!completed.Contains(StatisticsKey))
            {
                var detailsFile = await DownloadAsync(execution, detailsDataset, counters, cancellationToken);
                files.Add(detailsFile.FilePath);

                await runner.SetStageAsync(execution, "StreamingStatistics", cancellationToken);
                await ImportStatisticsAsync(execution, detailsDataset, detailsFile, scope.State, counters, cancellationToken);
            }

            await runner.CompleteAsync(execution, counters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await runner.CancelledAsync(execution, "TSE results import was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TSE results import failed for execution {ExecutionId}.", executionId);
            await runner.FailedAsync(execution, ex.Message);
        }
        finally
        {
            foreach (var file in files)
            {
                fileStore.TryDelete(file);
            }
        }
    }

    private async Task<CachedDownload> DownloadAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        Counters counters,
        CancellationToken ct)
    {
        await runner.SetStageAsync(execution, $"Downloading:{dataset.Kind}", ct);

        var downloaded = await provider.Download(execution.Id, dataset, ct);
        counters.BytesDownloaded += downloaded.SizeBytes;

        var provenance = DatasetProvenance.Create(
            execution.Id,
            "TSE",
            dataset.DatasetIdentifier,
            dataset.SourceUri.ToString(),
            dataset.Version,
            dataset.Format,
            Path.GetFileName(downloaded.FilePath));

        provenance.SetContentHash(downloaded.Sha256);
        provenance.SetFileSizeBytes(downloaded.SizeBytes);
        provenance.SetETag(downloaded.ETag);
        provenance.SetSourceLastModifiedAt(downloaded.LastModified?.UtcDateTime);
        await provenanceRepository.Add(provenance, ct);
        await unityOfWork.Commit();

        return downloaded;
    }

    private async Task ImportSectionsAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        CachedDownload downloaded,
        string? state,
        Counters counters,
        CancellationToken ct)
    {
        var batchSize = Math.Max(100, options.Value.ResultBatchSize);
        var buffer = new List<TsePollingSectionRecord>(batchSize);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var parsed in provider.StreamPollingSectionResults(downloaded.FilePath, dataset, state, ct))
        {
            ct.ThrowIfCancellationRequested();

            var record = parsed.Record;
            if (record is null)
            {
                counters.Rejected++;
                continue;
            }

            var key = $"{record.StateAbbreviation}/{record.ZoneNumber}/{record.SectionNumber}";
            if (!seen.Add(key))
            {
                continue;
            }

            buffer.Add(new TsePollingSectionRecord(
                record.StateAbbreviation,
                record.MunicipalityTseCode,
                record.ZoneNumber,
                record.SectionNumber,
                record.PollingPlaceCode,
                record.PollingPlaceName,
                record.PollingPlaceAddress));

            counters.Processed++;

            if (buffer.Count < batchSize)
            {
                continue;
            }

            var result = await resultsPersistence.UpsertPollingSections(buffer, ct);
            Apply(result, counters);
            buffer.Clear();
            await runner.ReportProgressAsync(execution, "StreamingSections", counters, ct);
        }

        if (buffer.Count > 0)
        {
            var result = await resultsPersistence.UpsertPollingSections(buffer, ct);
            Apply(result, counters);
        }

        await runner.CheckpointAsync(
            execution,
            SectionsKey,
            $"{{\"sections\":{counters.Persisted}}}",
            counters.Persisted,
            counters,
            ct);
    }

    private async Task ImportResultsAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        CachedDownload downloaded,
        string? state,
        Counters counters,
        CancellationToken ct)
    {
        var batchSize = Math.Max(100, options.Value.ResultBatchSize);
        var buffer = new List<TsePollingSectionResultRecord>(batchSize);
        var provenanceId = await GetProvenanceIdAsync(execution.Id, dataset.DatasetIdentifier, ct);
        var unresolvedSamples = new List<string>();

        await foreach (var parsed in provider.StreamPollingSectionResults(downloaded.FilePath, dataset, state, ct))
        {
            ct.ThrowIfCancellationRequested();

            var record = parsed.Record;
            if (record is null)
            {
                counters.Rejected++;
                continue;
            }

            buffer.Add(record);

            if (buffer.Count < batchSize)
            {
                continue;
            }

            await FlushResultsAsync(execution, buffer, provenanceId, counters, unresolvedSamples, ct);
            buffer.Clear();
            await runner.ReportProgressAsync(execution, "StreamingResults", counters, ct);
        }

        if (buffer.Count > 0)
        {
            await FlushResultsAsync(execution, buffer, provenanceId, counters, unresolvedSamples, ct);
        }

        if (unresolvedSamples.Count > 0)
        {
            await runner.RecordRejectionsAsync(
                execution.Id,
                dataset.DatasetIdentifier,
                unresolvedSamples.Select(s => new RejectedRecord("Unresolved result reference.", s, null)).ToList(),
                ct);
        }

        await runner.CheckpointAsync(
            execution,
            ResultsKey,
            $"{{\"results\":{counters.Persisted}}}",
            counters.Persisted,
            counters,
            ct);
    }

    private async Task FlushResultsAsync(
        ImportExecution execution,
        List<TsePollingSectionResultRecord> buffer,
        Guid provenanceId,
        Counters counters,
        List<string> unresolvedSamples,
        CancellationToken ct)
    {
        // A batch is grouped by election identity because one archive may publish more than one
        // election (federal and state), and each election owns its own rounds and contests.
        foreach (var group in buffer.GroupBy(r => r.Election.TseCode, StringComparer.Ordinal))
        {
            var first = group.First().Election;
            var electionId = await EnsureMetadataAsync(
                first,
                group.Select(r => (r.OfficeCode, r.OfficeName, r.StateAbbreviation)).Distinct(),
                ct);

            var merge = await resultsPersistence.UpsertPollingSectionResults(
                electionId,
                first.Round,
                provenanceId,
                group.ToList(),
                ct);

            Apply(merge, counters, unresolvedSamples);
        }
    }

    private async Task ImportStatisticsAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        CachedDownload downloaded,
        string? state,
        Counters counters,
        CancellationToken ct)
    {
        var batchSize = Math.Max(100, options.Value.StatisticsBatchSize);
        var buffer = new List<TsePollingSectionStatisticsRecord>(batchSize);
        var provenanceId = await GetProvenanceIdAsync(execution.Id, dataset.DatasetIdentifier, ct);

        await foreach (var parsed in provider.StreamPollingSectionStatistics(downloaded.FilePath, dataset, state, ct))
        {
            ct.ThrowIfCancellationRequested();

            var record = parsed.Record;
            if (record is null)
            {
                counters.Rejected++;
                continue;
            }

            buffer.Add(record);

            if (buffer.Count < batchSize)
            {
                continue;
            }

            await FlushStatisticsAsync(buffer, provenanceId, counters, ct);
            buffer.Clear();
            await runner.ReportProgressAsync(execution, "StreamingStatistics", counters, ct);
        }

        if (buffer.Count > 0)
        {
            await FlushStatisticsAsync(buffer, provenanceId, counters, ct);
        }

        await runner.CheckpointAsync(
            execution,
            StatisticsKey,
            $"{{\"statistics\":{counters.Persisted}}}",
            counters.Persisted,
            counters,
            ct);
    }

    private async Task FlushStatisticsAsync(
        List<TsePollingSectionStatisticsRecord> buffer,
        Guid provenanceId,
        Counters counters,
        CancellationToken ct)
    {
        foreach (var group in buffer.GroupBy(r => r.Election.TseCode, StringComparer.Ordinal))
        {
            var first = group.First().Election;
            var electionId = await EnsureMetadataAsync(
                first,
                group.Select(r => (r.OfficeCode, r.OfficeName, r.StateAbbreviation)).Distinct(),
                ct);

            var merge = await resultsPersistence.UpsertPollingSectionStatistics(
                electionId,
                first.Round,
                provenanceId,
                group.ToList(),
                ct);

            Apply(merge, counters, null);
        }
    }

    /// <summary>
    /// Ensures the election, its round and the contests present in the batch exist. Ensured
    /// identities are cached for the duration of the execution. Presidential contests
    /// (offices 1 and 2) are national and have no state scope; every other office is scoped
    /// to the state published on the row.
    /// </summary>
    private async Task<Guid> EnsureMetadataAsync(
        TseElectionIdentity identity,
        IEnumerable<(int OfficeCode, string OfficeName, string State)> contests,
        CancellationToken ct)
    {
        if (!_elections.TryGetValue(identity.TseCode, out var electionId))
        {
            electionId = await electoralPersistence.EnsureElection(
                identity.TseCode,
                identity.Year,
                identity.TypeCode,
                identity.TypeName,
                identity.Name,
                identity.ElectionDate,
                ct);

            _elections[identity.TseCode] = electionId;
        }

        var roundId = await electoralPersistence.EnsureElectionRound(electionId, identity.Round, ct);

        foreach (var (officeCode, officeName, state) in contests)
        {
            var scope = officeCode is 1 or 2 ? null : state;
            var key = $"{identity.TseCode}:{identity.Round}:{officeCode}:{scope}";
            if (!_contests.Add(key))
            {
                continue;
            }

            await electoralPersistence.EnsureContest(roundId, officeCode, officeName, scope, ct);
        }

        return electionId;
    }

    private async Task<Guid> GetProvenanceIdAsync(Guid executionId, string datasetIdentifier, CancellationToken ct)
    {
        var provenance = await provenanceRepository.GetLatest("TSE", datasetIdentifier, ct);
        return provenance?.Id ?? Guid.Empty;
    }

    private static void Apply(PersistenceResult result, Counters counters)
    {
        counters.Persisted += result.Inserted + result.Updated;
        counters.Skipped += result.Skipped;
        counters.Batches++;
    }

    private static void Apply(ResultMerge merge, Counters counters, List<string>? unresolvedSamples)
    {
        counters.Persisted += merge.Inserted + merge.Updated;
        counters.Skipped += merge.Unresolved;
        counters.Batches++;

        if (unresolvedSamples is not null && merge.UnresolvedSamples.Count > 0 && unresolvedSamples.Count < 50)
        {
            unresolvedSamples.AddRange(merge.UnresolvedSamples.Take(50 - unresolvedSamples.Count));
        }
    }
}
