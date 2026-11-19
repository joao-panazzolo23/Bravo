using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

public sealed class ImportJobExecutor(
    IImportJobRepository jobRepository,
    IImportWorkUnitRepository workUnits,
    IEnumerable<IImportPipeline> pipelines,
    ImportRetryPolicy retryPolicy,
    ILogger<ImportJobExecutor> logger
)
{
    public async Task ExecuteAsync(Guid jobId, string workerId, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetAsync(jobId, cancellationToken);

        if (job is null || job.IsTerminal)
            return;

        if (!await jobRepository.MarkRunningAsync(jobId, cancellationToken))
            return;

        using var jobScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ImportJobId"] = jobId,
            ["WorkerId"] = workerId
        });

        logger.LogInformation(
            "Import job execution started. JobId: {JobId}, WorkerId: {WorkerId}, BatchSize: {BatchSize}, MaxAttempts: {MaxAttempts}, StartedAt: {StartedAt}.",
            jobId, workerId, job.BatchSize, job.MaxAttempts, job.StartedAt);

        var leaseDuration = TimeSpan.FromSeconds(job.LeaseDurationSeconds);

        while (true)
        {
            var workUnit = await workUnits.ClaimNextAsync(jobId, workerId, leaseDuration, cancellationToken);

            if (workUnit is null)
                return;

            var pipeline = pipelines.SingleOrDefault(candidate => Matches(candidate.UnitKey, workUnit.UnitKey));

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

            if (await jobRepository.IsCancellationRequestedAsync(jobId, cancellationToken))
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
        using var unitScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ImportJobId"] = job.Id,
            ["WorkerId"] = workerId,
            ["WorkUnitId"] = workUnit.Id,
            ["WorkUnitKey"] = workUnit.UnitKey
        });

        logger.LogInformation(
            "Work unit claimed. JobId: {JobId}, WorkUnitId: {WorkUnitId}, Dataset: {Dataset}, Attempt: {Attempt}.",
            job.Id, workUnit.Id, workUnit.UnitKey, workUnit.AttemptCount + 1);

        var attemptCount = workUnit.AttemptCount;

        while (true)
        {
            try
            {
                await pipeline.ProcessAsync(job, workUnit, cancellationToken);

                // Pipelines that own their persistence never touch the batch store, so the
                // unit must be completed here; otherwise its lease expires and it re-runs
                // forever.
                await workUnits.CompleteAsync(workUnit.Id, workUnit.FencingToken, cancellationToken);

                await LogUnitCompletionAsync(job, workUnit, cancellationToken);
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
                        "Import work unit failed permanently. JobId: {JobId}, WorkUnitId: {WorkUnitId}, Dataset: {Dataset}, Attempts: {Attempts}, Error: {Error}.",
                        job.Id, workUnit.Id, workUnit.UnitKey, attempts, exception.Message);
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
                    "Transient import failure; retrying. JobId: {JobId}, WorkUnitId: {WorkUnitId}, Dataset: {Dataset}, Attempt: {Attempt}/{MaxAttempts}, RetryDelayMs: {DelayMs}.",
                    job.Id, workUnit.Id, workUnit.UnitKey, attempts, job.MaxAttempts, delay!.Value.TotalMilliseconds);

                await Task.Delay(delay.Value, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Logs completion with job-level counters so a reader can follow overall progress purely
    /// from the log stream.
    /// </summary>
    private async Task LogUnitCompletionAsync(
        ImportJob job,
        ImportWorkUnit workUnit,
        CancellationToken cancellationToken)
    {
        var units = await workUnits.GetByJobAsync(job.Id, cancellationToken);

        var total = units.Count;
        var completed = units.Count(unit => unit.Status == ImportWorkUnitStatus.Completed);
        var failed = units.Count(unit => unit.Status == ImportWorkUnitStatus.Failed);
        var percentage = total == 0 ? 0d : (double)completed / total * 100;

        logger.LogInformation(
            "Work unit completed. JobId: {JobId}, WorkUnitId: {WorkUnitId}, Dataset: {Dataset}, CompletedWorkUnits: {CompletedWorkUnits}, FailedWorkUnits: {FailedWorkUnits}, TotalWorkUnits: {TotalWorkUnits}, ProgressPercent: {ProgressPercent:F2}.",
            job.Id, workUnit.Id, workUnit.UnitKey, completed, failed, total, percentage);
    }

    /// <summary>
    /// A pipeline is registered by stage, while work-unit keys are scoped
    /// (<c>Stage:Year[:State]</c>). A candidate matches when the key equals the stage or
    /// begins with it followed by the scope separator.
    /// </summary>
    private static bool Matches(string pipelineUnitKey, string workUnitKey)
        => string.Equals(pipelineUnitKey, workUnitKey, StringComparison.Ordinal)
           || workUnitKey.StartsWith(pipelineUnitKey + ":", StringComparison.Ordinal);
}
