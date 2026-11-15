using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Acquisition.Handlers;

/// <summary>
/// Imports FGV complementary reference data: the official IBGE/TSE municipality crosswalk.
/// The crosswalk is required to resolve TSE municipality codes into the IBGE-based geography
/// used by electoral results.
/// </summary>
public class FgvCrosswalkImportHandler(
    ImportExecutionRunner runner,
    IDatasetProvenanceRepository provenanceRepository,
    IFgvProvider provider,
    IGeographyPersistenceService persistenceService,
    ITemporaryFileStore fileStore,
    IUnityOfWork unityOfWork,
    ILogger<FgvCrosswalkImportHandler> logger)
{
    private const string DatasetKey = "fgv-municipality-crosswalk";
    private const int BatchSize = 1000;

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await runner.OpenAsync(executionId, cancellationToken);
        if (execution is null)
        {
            return;
        }

        var counters = new Counters();
        string? downloadedPath = null;

        try
        {
            // The crosswalk resolves TSE municipality codes into IBGE municipalities, so it is
            // only meaningful after the IBGE geography import has populated them.
            var readiness = await persistenceService.GetReadiness(cancellationToken);
            if (!readiness.IsReady)
            {
                await runner.FailedAsync(
                    execution,
                    "IBGE geography is not available. Run the IbgeGeography import before the FGV crosswalk.");
                return;
            }

            var dataset = await provider.GetDatasetInfo(cancellationToken);
            await runner.SetStageAsync(execution, "DownloadingCrosswalk", cancellationToken);

            var downloaded = await provider.Download(execution.Id, dataset, cancellationToken);
            downloadedPath = downloaded.FilePath;
            counters.BytesDownloaded += downloaded.SizeBytes;

            var provenance = DatasetProvenance.Create(
                execution.Id,
                "FGV",
                DatasetKey,
                dataset.SourceUri.ToString(),
                dataset.Version,
                dataset.Format,
                Path.GetFileName(downloaded.FilePath));

            provenance.SetContentHash(downloaded.Sha256);
            provenance.SetFileSizeBytes(downloaded.SizeBytes);
            provenance.SetETag(downloaded.ETag);
            await provenanceRepository.Add(provenance, cancellationToken);
            await unityOfWork.Commit();

            var rejections = new List<RejectedRecord>();
            await runner.SetStageAsync(execution, "StreamingCrosswalk", cancellationToken);

            var buffer = new List<MunicipalityCrosswalkRecord>(BatchSize);

            await foreach (var parsed in provider.StreamMunicipalities(downloaded.FilePath, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (parsed.Record is null)
                {
                    rejections.Add(new RejectedRecord("Unparseable crosswalk row.", parsed.SourceReference, null));
                    continue;
                }

                buffer.Add(new MunicipalityCrosswalkRecord(
                    parsed.Record.StateAbbreviation,
                    parsed.Record.TseCode,
                    parsed.Record.IbgeCode,
                    parsed.Record.MunicipalityName,
                    parsed.Record.MesoRegionName,
                    parsed.Record.MicroRegionName));

                if (buffer.Count < BatchSize)
                {
                    continue;
                }

                await PersistBatchAsync(execution, buffer, counters, cancellationToken);
                buffer.Clear();
                await runner.ReportProgressAsync(execution, "StreamingCrosswalk", counters, cancellationToken);
            }

            if (buffer.Count > 0)
            {
                await PersistBatchAsync(execution, buffer, counters, cancellationToken);
            }

            counters.Rejected = rejections.Count;
            await runner.RecordRejectionsAsync(execution.Id, DatasetKey, rejections, cancellationToken);
            await runner.CheckpointAsync(
                execution,
                DatasetKey,
                $"{{\"crosswalk\":{counters.Persisted}}}",
                counters.Persisted,
                counters,
                cancellationToken);

            await runner.CompleteAsync(execution, counters, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await runner.CancelledAsync(execution, "FGV crosswalk import was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FGV crosswalk import failed for execution {ExecutionId}.", executionId);
            await runner.FailedAsync(execution, ex.Message);
        }
        finally
        {
            fileStore.TryDelete(downloadedPath);
        }
    }

    private async Task PersistBatchAsync(
        ImportExecution execution,
        List<MunicipalityCrosswalkRecord> buffer,
        Counters counters,
        CancellationToken ct)
    {
        var result = await persistenceService.UpsertMunicipalityTseCodes(buffer, ct);
        counters.Processed += buffer.Count;
        counters.Persisted += result.Inserted + result.Updated;
        counters.Skipped += result.Skipped;
        counters.Batches++;
    }
}
