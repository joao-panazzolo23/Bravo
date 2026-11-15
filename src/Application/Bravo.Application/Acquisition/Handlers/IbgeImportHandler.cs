using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Acquisition.Handlers;

public class IbgeImportHandler(
    ImportExecutionRunner runner,
    IDatasetProvenanceRepository provenanceRepository,
    IIbgeProvider ibgeProvider,
    IGeographyPersistenceService persistenceService,
    ITemporaryFileStore fileStore,
    IUnityOfWork unityOfWork,
    IOptions<AcquisitionOptions> options,
    ILogger<IbgeImportHandler> logger
)
{
    private const string StatesKey = "ibge-states";
    private const string MunicipalitiesKey = "ibge-municipalities";
    private const int StateBatchSize = 100;
    private const int MunicipalityBatchSize = 1000;

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await runner.OpenAsync(executionId, cancellationToken);
        if (execution is null)
        {
            return;
        }

        var counters = new Counters();
        var downloadedFiles = new List<string>();
        Task<CachedDownload>? pendingStates = null;
        Task<CachedDownload>? pendingMunicipalities = null;

        try
        {
            var datasetInfo = await ibgeProvider.GetDatasetInfo(cancellationToken);
            var completed = await runner.GetCompletedKeysAsync(execution, cancellationToken);

            // States and municipalities are independent datasets. They are downloaded
            // concurrently within the configured bound, but persisted sequentially because
            // municipalities reference the states.
            await runner.SetStageAsync(execution, "DownloadingGeography", cancellationToken);

            var allowConcurrent = options.Value.MaxConcurrentDownloads >= 2;
            if (!completed.Contains(StatesKey))
            {
                pendingStates = ibgeProvider.DownloadStates(execution.Id, null, cancellationToken);
            }

            if (!completed.Contains(MunicipalitiesKey))
            {
                pendingMunicipalities = allowConcurrent
                    ? ibgeProvider.DownloadMunicipalities(execution.Id, null, cancellationToken)
                    : null;
            }

            if (pendingStates is not null)
            {
                var statesFile = await pendingStates;
                downloadedFiles.Add(statesFile.FilePath);
                counters.BytesDownloaded += statesFile.SizeBytes;

                var provenance = DatasetProvenance.Create(
                    execution.Id,
                    "IBGE",
                    "ibge-states",
                    ibgeProvider.GetSourceUri(DatasetScope.States).ToString(),
                    datasetInfo.Version,
                    datasetInfo.Format,
                    Path.GetFileName(statesFile.FilePath));

                provenance.SetContentHash(statesFile.Sha256);
                provenance.SetFileSizeBytes(statesFile.SizeBytes);
                await provenanceRepository.Add(provenance, cancellationToken);
                await unityOfWork.Commit();

                await runner.SetStageAsync(execution, "StreamingStates", cancellationToken);
                await ImportStatesAsync(execution, statesFile.FilePath, counters, cancellationToken);
            }
            else
            {
                logger.LogInformation("Skipping states dataset: checkpoint already completed for {ExecutionId}.", executionId);
            }

            if (pendingMunicipalities is not null || (pendingStates is null && !completed.Contains(MunicipalitiesKey)))
            {
                var municipalitiesFile = pendingMunicipalities is not null
                    ? await pendingMunicipalities
                    : await ibgeProvider.DownloadMunicipalities(execution.Id, null, cancellationToken);

                downloadedFiles.Add(municipalitiesFile.FilePath);
                counters.BytesDownloaded += municipalitiesFile.SizeBytes;

                var provenance = DatasetProvenance.Create(
                    execution.Id,
                    "IBGE",
                    "ibge-municipalities",
                    ibgeProvider.GetSourceUri(DatasetScope.Municipalities).ToString(),
                    datasetInfo.Version,
                    datasetInfo.Format,
                    Path.GetFileName(municipalitiesFile.FilePath));

                provenance.SetContentHash(municipalitiesFile.Sha256);
                provenance.SetFileSizeBytes(municipalitiesFile.SizeBytes);
                await provenanceRepository.Add(provenance, cancellationToken);
                await unityOfWork.Commit();

                await runner.SetStageAsync(execution, "StreamingMunicipalities", cancellationToken);
                await ImportMunicipalitiesAsync(execution, municipalitiesFile.FilePath, counters, cancellationToken);
            }
            else
            {
                logger.LogInformation("Skipping municipalities dataset: checkpoint already completed for {ExecutionId}.", executionId);
            }

            await runner.CompleteAsync(execution, counters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await runner.CancelledAsync(execution, "IBGE import was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IBGE import failed for execution {ExecutionId}.", executionId);
            await runner.FailedAsync(execution, ex.Message);
        }
        finally
        {
            foreach (var file in downloadedFiles)
            {
                fileStore.TryDelete(file);
            }
        }
    }

    private async Task ImportStatesAsync(
        ImportExecution execution,
        string filePath,
        Counters counters,
        CancellationToken ct)
    {
        var rejections = new List<RejectedRecord>();
        var statesPersisted = 0;

        await foreach (var batch in BatchStream.Chunk(
            SelectRecords(ibgeProvider.StreamStates(filePath, ct), counters, rejections),
            StateBatchSize,
            ct))
        {
            ct.ThrowIfCancellationRequested();

            var result = await persistenceService.UpsertStates(batch, ct);
            counters.Processed += batch.Count;
            counters.Persisted += result.Inserted + result.Updated;
            counters.Skipped += result.Skipped;
            counters.Batches++;
            statesPersisted += result.Inserted + result.Updated;

            await runner.ReportProgressAsync(execution, "StreamingStates", counters, ct);
        }

        counters.Rejected += rejections.Count;
        await runner.RecordRejectionsAsync(execution.Id, StatesKey, rejections, ct);
        await runner.CheckpointAsync(
            execution,
            StatesKey,
            $"{{\"states\":{statesPersisted}}}",
            statesPersisted,
            counters,
            ct);
    }

    private async Task ImportMunicipalitiesAsync(
        ImportExecution execution,
        string filePath,
        Counters counters,
        CancellationToken ct)
    {
        var rejections = new List<RejectedRecord>();
        var persisted = 0;

        await foreach (var batch in BatchStream.Chunk(
            SelectRecords(ibgeProvider.StreamMunicipalities(filePath, ct), counters, rejections),
            MunicipalityBatchSize,
            ct))
        {
            ct.ThrowIfCancellationRequested();

            var result = await persistenceService.UpsertMunicipalities(batch, ct);
            counters.Processed += batch.Count;
            counters.Persisted += result.Inserted + result.Updated;
            counters.Skipped += result.Skipped;
            counters.Batches++;
            persisted += result.Inserted + result.Updated;

            await runner.ReportProgressAsync(execution, "StreamingMunicipalities", counters, ct);
        }

        counters.Rejected += rejections.Count;
        await runner.RecordRejectionsAsync(execution.Id, MunicipalitiesKey, rejections, ct);
        await runner.CheckpointAsync(
            execution,
            MunicipalitiesKey,
            $"{{\"municipalities\":{persisted}}}",
            persisted,
            counters,
            ct);
    }

    private static async IAsyncEnumerable<T> SelectRecords<T>(
        IAsyncEnumerable<ParsedRecord<T>> source,
        Counters counters,
        List<RejectedRecord> rejections,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var parsed in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (parsed.Record is null)
            {
                rejections.Add(new RejectedRecord("Unparseable record.", parsed.SourceReference, null));
                continue;
            }

            yield return parsed.Record;
        }
    }
}
