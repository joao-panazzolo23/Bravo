using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

public sealed class ImportJobExecutor(
    IImportJobRepository jobs,
    IImportWorkUnitRepository workUnits,
    IEnumerable<IImportPipeline> pipelines,
    ImportRetryPolicy retryPolicy,
    ILogger<ImportJobExecutor> logger)
{
    public async Task ExecuteAsync(Guid jobId, string workerId, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetAsync(jobId, cancellationToken);

        if (job is null || job.IsTerminal)
            return;

        if (!await jobs.MarkRunningAsync(jobId, cancellationToken))
            return;

        var leaseDuration = TimeSpan.FromSeconds(job.LeaseDurationSeconds);

        while (true)
        {
            var workUnit = await workUnits.ClaimNextAsync(jobId, workerId, leaseDuration, cancellationToken);

            if (workUnit is null)
                return;

            var pipeline = pipelines.SingleOrDefault(candidate => candidate.UnitKey == workUnit.UnitKey);

            if (pipeline is null)
            {
                await workUnits.FailAsync(
                    jobId,
                    workUnit.Id,
                    workUnit.FencingToken,
                    $"No import pipeline is registered for work unit '{workUnit.UnitKey}'.",
                    cancellationToken);
                continue;
            }

            await ProcessWorkUnitAsync(job, workUnit, pipeline, workerId, leaseDuration, cancellationToken);

            if (await jobs.IsCancellationRequestedAsync(jobId, cancellationToken))
                return;
        }
    }

    private async Task ProcessWorkUnitAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        IImportPipeline pipeline,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var attemptCount = workUnit.AttemptCount;

        while (true)
        {
            try
            {
                await pipeline.ProcessAsync(job, workUnit, cancellationToken);
                return;
            }
            catch (ImportLeaseLostException exception)
            {
                logger.LogWarning(
                    exception,
                    "Worker {WorkerId} lost the lease for import work unit {WorkUnitId}.",
                    workerId,
                    workUnit.Id);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await workUnits.ReleaseAsync(
                    workUnit.Id,
                    workerId,
                    workUnit.FencingToken,
                    "Worker shutting down.",
                    CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                var isTransient = ImportFailureClassifier.IsTransient(exception);
                attemptCount++;
                var delay = isTransient
                    ? retryPolicy.GetDelay(attemptCount, ImportFailureClassifier.GetRetryAfter(exception))
                    : (TimeSpan?)null;

                var attempts = await workUnits.RecordAttemptAsync(
                    workUnit.Id,
                    workUnit.FencingToken,
                    exception.Message,
                    delay is null ? null : DateTime.UtcNow.Add(delay.Value),
                    cancellationToken);

                if (attempts == 0)
                {
                    logger.LogWarning(
                        "Worker {WorkerId} could not record a failure for import work unit {WorkUnitId} because ownership changed.",
                        workerId,
                        workUnit.Id);
                    return;
                }

                if (!isTransient || attempts >= job.MaxAttempts)
                {
                    await workUnits.FailAsync(
                        job.Id,
                        workUnit.Id,
                        workUnit.FencingToken,
                        exception.Message,
                        cancellationToken);

                    logger.LogWarning(
                        exception,
                        "Import work unit {WorkUnitId} failed after {Attempts} attempt(s).",
                        workUnit.Id,
                        attempts);
                    return;
                }

                if (!await workUnits.RenewLeaseAsync(
                    workUnit.Id,
                    workerId,
                    workUnit.FencingToken,
                    leaseDuration,
                    cancellationToken))
                {
                    logger.LogWarning(
                        "Worker {WorkerId} lost the lease for import work unit {WorkUnitId} before retrying.",
                        workerId,
                        workUnit.Id);
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Transient import failure for work unit {WorkUnitId}. Retrying in {DelayMs} ms after attempt {Attempts}.",
                    workUnit.Id,
                    delay!.Value.TotalMilliseconds,
                    attempts);

                await Task.Delay(delay.Value, cancellationToken);
            }
        }
    }
}
