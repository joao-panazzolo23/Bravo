using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Acquisition.Handlers;

/// <summary>
/// Imports TSE election metadata, political parties and candidates for one election year.
///
/// The same candidate archive publishes the election, round and contest metadata, so one
/// download feeds both the metadata and the candidate stages. Each stage is checkpointed
/// only after its batches were committed, and the archive is removed when the execution ends.
/// </summary>
public class TseCandidatesImportHandler(
    ImportExecutionRunner runner,
    IDatasetProvenanceRepository provenanceRepository,
    ITseProvider provider,
    IElectoralPersistenceService persistenceService,
    ITemporaryFileStore fileStore,
    IUnityOfWork unityOfWork,
    Microsoft.Extensions.Options.IOptions<Options.AcquisitionOptions> options,
    ILogger<TseCandidatesImportHandler> logger)
{
    private const string MetadataKey = "tse-election-metadata";
    private const string CandidatesKey = "tse-candidates";

    public async Task ExecuteAsync(Guid executionId, ImportScope scope, CancellationToken cancellationToken)
    {
        if (scope.Year is not { } year)
        {
            logger.LogError("TSE candidate import requires an election year.");
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
            if (completed.Contains(MetadataKey) && completed.Contains(CandidatesKey))
            {
                logger.LogInformation("TSE candidates for {Year} already checkpointed in execution {ExecutionId}.", year, executionId);
                await runner.CompleteAsync(execution, counters, cancellationToken);
                return;
            }

            await runner.SetStageAsync(execution, "ResolvingDataset", cancellationToken);
            var dataset = await provider.ResolveDataset(TseDatasetKind.Candidates, year, cancellationToken);

            await runner.SetStageAsync(execution, "DownloadingCandidates", cancellationToken);
            var downloaded = await provider.Download(execution.Id, dataset, cancellationToken);
            files.Add(downloaded.FilePath);
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
            await provenanceRepository.Add(provenance, cancellationToken);
            await unityOfWork.Commit();

            var rejections = new List<RejectedRecord>();

            if (!completed.Contains(MetadataKey))
            {
                await runner.SetStageAsync(execution, "StreamingElectionMetadata", cancellationToken);
                await ImportMetadataAsync(execution, dataset, downloaded, scope.State, counters, rejections, cancellationToken);
            }

            if (!completed.Contains(CandidatesKey))
            {
                await runner.SetStageAsync(execution, "StreamingCandidates", cancellationToken);
                await ImportCandidatesAsync(execution, dataset, downloaded, scope.State, counters, rejections, cancellationToken);
            }

            counters.Rejected = rejections.Count;
            await runner.RecordRejectionsAsync(execution.Id, dataset.DatasetIdentifier, rejections, cancellationToken);
            await runner.CompleteAsync(execution, counters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await runner.CancelledAsync(execution, "TSE candidate import was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TSE candidate import failed for execution {ExecutionId}.", executionId);
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

    private async Task ImportMetadataAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        CachedDownload downloaded,
        string? state,
        Counters counters,
        List<RejectedRecord> rejections,
        CancellationToken ct)
    {
        var buffer = new List<TseElectionMetadataRecord>();

        await foreach (var parsed in provider.StreamElectionMetadata(downloaded.FilePath, dataset, state, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (parsed.Record is null)
            {
                rejections.Add(new RejectedRecord("Unparseable election metadata row.", parsed.SourceReference, null));
                continue;
            }

            buffer.Add(parsed.Record);
            counters.Processed++;

            if (buffer.Count < 50)
            {
                continue;
            }

            await PersistMetadataAsync(buffer, ct);
            counters.Persisted += buffer.Count;
            counters.Batches++;
            buffer.Clear();
            await runner.ReportProgressAsync(execution, "StreamingElectionMetadata", counters, ct);
        }

        if (buffer.Count > 0)
        {
            await PersistMetadataAsync(buffer, ct);
            counters.Persisted += buffer.Count;
            counters.Batches++;
        }

        await runner.CheckpointAsync(
            execution,
            MetadataKey,
            $"{{\"elections\":{counters.Persisted}}}",
            counters.Persisted,
            counters,
            ct);
    }

    private async Task PersistMetadataAsync(List<TseElectionMetadataRecord> records, CancellationToken ct)
    {
        foreach (var election in records)
        {
            var electionId = await persistenceService.EnsureElection(
                election.TseCode,
                election.Year,
                election.TypeCode,
                election.TypeName,
                election.Name,
                election.ElectionDate,
                ct);

            var roundId = await persistenceService.EnsureElectionRound(electionId, election.Round, ct);

            foreach (var contest in election.Contests)
            {
                await persistenceService.EnsureContest(
                    roundId,
                    contest.OfficeCode,
                    contest.OfficeName,
                    contest.StateAbbreviation,
                    ct);
            }
        }
    }

    private async Task ImportCandidatesAsync(
        ImportExecution execution,
        TseDatasetInfo dataset,
        CachedDownload downloaded,
        string? state,
        Counters counters,
        List<RejectedRecord> rejections,
        CancellationToken ct)
    {
        var batchSize = Math.Max(100, options.Value.CandidateBatchSize);
        var buffer = new List<TseCandidateRecord>(batchSize);
        var parties = new Dictionary<int, PartyReference>();
        var electionIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var geographic = await persistenceService.GetGeographicLookup(ct);

        await foreach (var parsed in provider.StreamCandidates(downloaded.FilePath, dataset, state, ct))
        {
            ct.ThrowIfCancellationRequested();

            var candidate = parsed.Record;
            if (candidate is null)
            {
                rejections.Add(new RejectedRecord("Unparseable candidate row.", parsed.SourceReference, null));
                continue;
            }

            parties[candidate.PartyNumber] = new PartyReference(
                candidate.PartyNumber,
                candidate.PartyAcronym,
                candidate.PartyName);

            buffer.Add(candidate);
            counters.Processed++;

            if (buffer.Count < batchSize)
            {
                continue;
            }

            await PersistCandidatesAsync(buffer, parties, electionIds, geographic, counters, ct);
            buffer.Clear();
            await runner.ReportProgressAsync(execution, "StreamingCandidates", counters, ct);
        }

        if (buffer.Count > 0)
        {
            await PersistCandidatesAsync(buffer, parties, electionIds, geographic, counters, ct);
        }

        await runner.CheckpointAsync(
            execution,
            CandidatesKey,
            $"{{\"candidates\":{counters.Persisted},\"elections\":{electionIds.Count}}}",
            counters.Persisted,
            counters,
            ct);
    }

    /// <summary>
    /// Persists one candidate batch. A single year archive may publish several elections
    /// (federal, state, supplementary), so the batch is grouped by the election code carried
    /// by each row and every group is written against its own election.
    /// </summary>
    private async Task PersistCandidatesAsync(
        List<TseCandidateRecord> buffer,
        Dictionary<int, PartyReference> parties,
        Dictionary<string, Guid> electionIds,
        GeographicLookup geographic,
        Counters counters,
        CancellationToken ct)
    {
        foreach (var group in buffer.GroupBy(c => c.ElectionTseCode))
        {
            var first = group.First();

            if (!electionIds.TryGetValue(group.Key, out var electionId))
            {
                electionId = await persistenceService.EnsureElection(
                    group.Key,
                    first.ElectionYear,
                    first.TypeCode,
                    first.TypeName,
                    first.ElectionName,
                    first.ElectionDate,
                    ct);

                await persistenceService.EnsureElectionRound(electionId, first.Round, ct);
                electionIds[group.Key] = electionId;
            }

            var partyIds = await persistenceService.EnsureParties(parties.Values.ToList(), ct);
            var result = await persistenceService.UpsertCandidates(
                electionId,
                group.Select(c => new CandidateReferenceRecord(
                    c.TseSequential,
                    c.Number,
                    c.Name,
                    c.BallotName,
                    c.OfficeCode,
                    c.OfficeName,
                    c.PartyNumber,
                    c.StateAbbreviation,
                    c.SituationCode,
                    c.SituationDescription,
                    c.BirthDate)).ToList(),
                partyIds,
                geographic.StatesByAbbreviation,
                ct);

            counters.Persisted += result.Inserted + result.Updated;
            counters.Skipped += result.Skipped;
            counters.Batches++;
        }
    }
}
