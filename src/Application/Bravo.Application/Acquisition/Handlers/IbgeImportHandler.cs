using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Acquisition.Handlers;

public class IbgeImportHandler(
    IImportExecutionRepository executionRepository,
    IImportCheckpointRepository checkpointRepository,
    IDatasetProvenanceRepository provenanceRepository,
    IIbgeProvider ibgeProvider,
    IGeographyPersistenceService persistenceService,
    IUnityOfWork unityOfWork,
    ILogger<IbgeImportHandler> logger
)
{
    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await executionRepository.GetById(executionId, cancellationToken);
        if (execution is null)
        {
            logger.LogError("Execution with id {ExecutionId} not found.", executionId);
            return;
        }

        if (execution.Status is ImportStatus.CancellationRequested)
        {
            execution.MarkCancelled("Cancelled prior to execution start.");
            await executionRepository.Update(execution, CancellationToken.None);
            await unityOfWork.Commit();
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            execution.MarkRunning();
            execution.UpdateProgress("Starting", 0, 0);
            await executionRepository.Update(execution, cancellationToken);
            await unityOfWork.Commit();

            var datasetInfo = await ibgeProvider.GetDatasetInfo(cancellationToken);
            var provenance = DatasetProvenance.Create(
                execution.Id,
                "IBGE",
                "ibge-localidades",
                datasetInfo.StatesUrl,
                datasetInfo.Version,
                datasetInfo.Format
            );
            await provenanceRepository.Add(provenance, cancellationToken);
            await unityOfWork.Commit();

            var statesCheckpoint = await checkpointRepository.GetLatest(execution.Id, "ibge-states", cancellationToken);
            var statesPersisted = statesCheckpoint?.RecordsCompleted ?? 0;

            if (statesCheckpoint is null)
            {
                execution.UpdateProgress("StreamingStates", 0, 0);
                await executionRepository.Update(execution, cancellationToken);
                await unityOfWork.Commit();

                var statesBatch = new List<IbgeStateRecord>(50);
                await foreach (var state in ibgeProvider.StreamStates(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    statesBatch.Add(state);
                    if (statesBatch.Count >= 50)
                    {
                        var inserted = await persistenceService.UpsertStates(statesBatch, cancellationToken);
                        statesPersisted += inserted;
                        statesBatch.Clear();
                    }
                }

                if (statesBatch.Count > 0)
                {
                    var inserted = await persistenceService.UpsertStates(statesBatch, cancellationToken);
                    statesPersisted += inserted;
                    statesBatch.Clear();
                }

                var statesCp = ImportCheckpoint.Create(execution.Id, "ibge-states", $"{{\"states\": {statesPersisted}}}", statesPersisted);
                await checkpointRepository.Save(statesCp, cancellationToken);
                execution.UpdateProgress("StatesCompleted", statesPersisted, statesPersisted);
                await executionRepository.Update(execution, cancellationToken);
                await unityOfWork.Commit();
            }

            var muniCheckpoint = await checkpointRepository.GetLatest(execution.Id, "ibge-municipalities", cancellationToken);
            var muniPersisted = muniCheckpoint?.RecordsCompleted ?? 0;

            if (muniCheckpoint is null)
            {
                execution.UpdateProgress("StreamingMunicipalities", statesPersisted, statesPersisted);
                await executionRepository.Update(execution, cancellationToken);
                await unityOfWork.Commit();

                var muniBatch = new List<IbgeMunicipalityRecord>(500);
                var muniProcessed = 0;

                await foreach (var muni in ibgeProvider.StreamMunicipalities(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    muniBatch.Add(muni);
                    muniProcessed++;

                    if (muniBatch.Count >= 500)
                    {
                        var inserted = await persistenceService.UpsertMunicipalities(muniBatch, cancellationToken);
                        muniPersisted += inserted;
                        muniBatch.Clear();

                        execution.UpdateProgress("StreamingMunicipalities", statesPersisted + muniProcessed, statesPersisted + muniPersisted);
                        await executionRepository.Update(execution, cancellationToken);
                        await unityOfWork.Commit();
                    }
                }

                if (muniBatch.Count > 0)
                {
                    var inserted = await persistenceService.UpsertMunicipalities(muniBatch, cancellationToken);
                    muniPersisted += inserted;
                    muniBatch.Clear();
                }

                var muniCp = ImportCheckpoint.Create(execution.Id, "ibge-municipalities", $"{{\"municipalities\": {muniPersisted}}}", muniPersisted);
                await checkpointRepository.Save(muniCp, cancellationToken);
                execution.UpdateProgress("MunicipalitiesCompleted", statesPersisted + muniProcessed, statesPersisted + muniPersisted);
                await executionRepository.Update(execution, cancellationToken);
                await unityOfWork.Commit();
            }

            execution.MarkCompleted();
            await executionRepository.Update(execution, cancellationToken);
            await unityOfWork.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            execution.MarkCancelled("Import was cancelled by request.");
            await executionRepository.Update(execution, CancellationToken.None);
            await unityOfWork.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import failed for execution {ExecutionId}", executionId);
            execution.MarkFailed(ex.Message);
            await executionRepository.Update(execution, CancellationToken.None);
            await unityOfWork.Commit();
        }
    }
}
