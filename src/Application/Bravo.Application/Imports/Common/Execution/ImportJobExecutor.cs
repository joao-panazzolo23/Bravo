using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Logging;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;

namespace Bravo.Application.Imports.Common.Execution;

/// <summary>
/// Runs one import job end to end: claim, execute, keep the lease alive, and settle the job row.
/// </summary>
/// <remarks>
/// This is the orchestration the hosted service used to own. Keeping it here means the scheduler
/// can be a plain dispatcher, and the lifecycle can be tested without a background service.
///
/// Cancellation is scoped to a single execution: the caller supplies the process token, and a
/// lost lease or an operator cancellation cancels that execution only. The heartbeat runs for
/// exactly as long as the foreground work, and is always awaited before the method returns.
/// </remarks>
public sealed class ImportJobExecutor(
    IImportJobRepository jobs,
    IEnumerable<IImportSource> sources,
    ImportRetryPolicy retryPolicy,
    IOptions<ImportEngineOptions> options,
    ILogger<ImportJobExecutor> logger)
{
    private readonly ImportEngineOptions _engine = options.Value;

    /// <summary>
    /// Attempts to run <paramref name="jobId"/> under <paramref name="workerId"/>.
    /// </summary>
    /// <remarks>
    /// Never throws for an expected lifecycle outcome (lost claim, cancellation, failed import);
    /// those are reported as an <see cref="ImportJobExecutionOutcome"/>. The caller decides what
    /// to do next, which is what keeps retry policy out of the scheduler.
    /// </remarks>
    public async Task<ImportJobExecutionResult> ExecuteAsync(
        Guid jobId,
        string workerId,
        CancellationToken stoppingToken)
    {
        ImportJob? job;

        try
        {
            job = await jobs.ClaimAsync(
                jobId,
                workerId,
                TimeSpan.FromSeconds(_engine.DefaultLeaseDurationSeconds),
                stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerClaimFailed(logger, workerId, jobId, exception);
            return new ImportJobExecutionResult(ImportJobExecutionOutcome.NotClaimed);
        }

        if (job is null)
            return new ImportJobExecutionResult(ImportJobExecutionOutcome.NotClaimed);

        var source = sources.FirstOrDefault(candidate => candidate.Key == job.Source);

        if (source is null)
        {
            await jobs.CompleteAsync(
                job.Id,
                workerId,
                ImportJobStatus.Failed,
                $"No import source is registered for '{job.Source}'.",
                CancellationToken.None);

            WorkerLog.UnregisteredSource(logger, job.Id, job.Source);

            return new ImportJobExecutionResult(ImportJobExecutionOutcome.Completed);
        }

        using var jobScope = logger.BeginJobScope(job.Id, job.Source, workerId);

        ImportLog.JobStarted(logger, job.Id, job.Source, job.Scope, job.AttemptCount);

        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var progress = new ImportProgressSink(
            jobs,
            job.Id,
            workerId,
            Microsoft.Extensions.Options.Options.Create(_engine),
            logger);

        var context = new ImportExecutionContext(
            job.Id,
            job.BatchSize,
            job.MaxParallelism,
            progress);

        var heartbeat = HeartbeatAsync(job.Id, workerId, progress, executionCts, stoppingToken);

        try
        {
            await source.ExecuteAsync(context, executionCts.Token);
            await progress.FlushAsync(CancellationToken.None);

            var finished = await jobs.GetAsync(job.Id, CancellationToken.None);

            var status = finished is { TotalFailedRecords: > 0 }
                ? ImportJobStatus.CompletedWithErrors
                : ImportJobStatus.Completed;

            await jobs.CompleteAsync(job.Id, workerId, status, error: null, CancellationToken.None);

            if (status == ImportJobStatus.CompletedWithErrors)
            {
                ImportLog.JobCompletedWithErrors(
                    logger, job.Id, finished?.TotalProcessedRecords, finished?.TotalFailedRecords);
            }
            else
            {
                ImportLog.JobCompleted(
                    logger, job.Id, finished?.TotalProcessedRecords, finished?.TotalFailedRecords);
            }

            return new ImportJobExecutionResult(
                ImportJobExecutionOutcome.Completed,
                finished?.TotalProcessedRecords ?? 0,
                finished?.TotalFailedRecords ?? 0);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await jobs.ReleaseAsync(
                job.Id, workerId, "Application is shutting down.", CancellationToken.None);

            ImportLog.JobReleased(logger, job.Id);

            return new ImportJobExecutionResult(ImportJobExecutionOutcome.ReleasedForShutdown);
        }
        catch (OperationCanceledException)
        {
            // The job was cancelled or lost its lease; the row is already in its final state.
            ImportLog.JobStopped(logger, job.Id);

            return new ImportJobExecutionResult(ImportJobExecutionOutcome.Stopped);
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(job, workerId, exception);

            return new ImportJobExecutionResult(ImportJobExecutionOutcome.Completed);
        }
        finally
        {
            // Stop the heartbeat before returning so no background loop outlives the job.
            executionCts.Cancel();
            await heartbeat;
        }
    }

    /// <summary>
    /// Renews the lease and turns a lost lease or an operator cancellation into cancellation of
    /// the running import, so shutdown is prompt rather than waiting for the current stage.
    /// </summary>
    private async Task HeartbeatAsync(
        Guid jobId,
        string workerId,
        ImportProgressSink progress,
        CancellationTokenSource executionCts,
        CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _engine.DefaultLeaseDurationSeconds / 3.0));

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await progress.FlushAsync(stoppingToken);

                var renewed = await RenewAsync(jobId, workerId, stoppingToken);

                if (renewed)
                    continue;

                WorkerLog.WorkerLeaseLost(logger, jobId, workerId);

                executionCts.Cancel();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WorkerLog.WorkerHeartbeatFailed(logger, jobId, exception);
        }
    }

    private async Task<bool> RenewAsync(
        Guid jobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await jobs.RenewLeaseAsync(
                jobId,
                workerId,
                TimeSpan.FromSeconds(_engine.DefaultLeaseDurationSeconds),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerLeaseRenewFailed(logger, jobId, exception);

            // A transient renewal failure is not proof the lease is gone; keep the job running.
            return true;
        }
    }

    private async Task HandleFailureAsync(ImportJob job, string workerId, Exception exception)
    {
        var transient = ImportFailureClassifier.IsTransient(exception);
        DateTime? retryAt = null;

        if (transient && job.AttemptCount < job.MaxAttempts)
        {
            var delay = retryPolicy.GetDelay(
                job.AttemptCount,
                ImportFailureClassifier.GetRetryAfter(exception));

            retryAt = DateTime.UtcNow.Add(delay);
        }

        ImportLog.JobFailed(
            logger,
            job.Id,
            job.Source,
            job.AttemptCount,
            job.MaxAttempts,
            retryAt,
            exception);

        await jobs.FailAsync(
            job.Id,
            workerId,
            exception.Message,
            retryAt,
            CancellationToken.None);
    }
}
