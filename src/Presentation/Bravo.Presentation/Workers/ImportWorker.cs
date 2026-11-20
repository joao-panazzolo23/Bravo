using System.Collections.Concurrent;
using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.Services;
using Bravo.Application.Logging;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.Options;

namespace Bravo.Presentation.Workers;

/// <summary>
/// Durable import scheduler: polls for jobs, claims them atomically, runs the matching
/// <see cref="IImportSource"/> and keeps the job lease alive while it runs.
/// </summary>
/// <remarks>
/// The worker owns only scheduling concerns: claim, heartbeat, cancellation, shutdown. What an
/// import actually does belongs to the source, which is resolved by key, so adding a provider
/// never changes this class.
/// </remarks>
public sealed class ImportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ImportWorkerOptions> workerOptions,
    IOptions<ImportEngineOptions> engineOptions,
    ILogger<ImportWorker> logger) : BackgroundService
{
    private readonly ImportWorkerOptions _worker = workerOptions.Value;
    private readonly ImportEngineOptions _engine = engineOptions.Value;
    private readonly ConcurrentDictionary<Guid, Task> _running = new();
    private readonly SemaphoreSlim _slots = new(workerOptions.Value.MaxConcurrentJobs);
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_worker.Enabled)
        {
            WorkerLog.WorkerDisabled(logger);
            return;
        }

        await RecoverAbandonedJobsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_worker.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                WorkerLog.WorkerPollingFailed(logger, exception);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await Task.WhenAll(_running.Values);
    }

    private async Task RecoverAbandonedJobsAsync(CancellationToken cancellationToken)
    {
        if (!_worker.RecoverOnStartup)
            return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

            var released = await jobs.ReleaseOwnedByAsync($"{Environment.MachineName}:", cancellationToken);

            if (released > 0)
            {
                WorkerLog.WorkerAbandonedJobsRecovered(logger, released, Environment.MachineName);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerReleaseAbandonedFailed(logger, exception);
        }
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

        var jobIds = await jobs.ListRecoverableIdsAsync(_worker.MaxJobsPerPoll, cancellationToken);

        foreach (var jobId in jobIds)
        {
            if (_running.ContainsKey(jobId))
                continue;

            if (!await _slots.WaitAsync(0, cancellationToken))
            {
                WorkerLog.WorkerIdle(logger, _workerId, _running.Count);
                break;
            }

            var task = RunJobAsync(jobId, cancellationToken);
            _running[jobId] = task;

            _ = task.ContinueWith(
                _ =>
                {
                    _running.TryRemove(jobId, out _);
                    _slots.Release();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        var sources = scope.ServiceProvider.GetServices<IImportSource>().ToArray();

        ImportJob? job;

        try
        {
            job = await jobs.ClaimAsync(
                jobId,
                _workerId,
                TimeSpan.FromSeconds(_engine.DefaultLeaseDurationSeconds),
                stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerClaimFailed(logger, _workerId, jobId, exception);
            return;
        }

        if (job is null)
            return;

        var source = sources.FirstOrDefault(candidate => candidate.Key == job.Source);

        if (source is null)
        {
            await jobs.CompleteAsync(
                job.Id,
                _workerId,
                ImportJobStatus.Failed,
                $"No import source is registered for '{job.Source}'.",
                CancellationToken.None);

            WorkerLog.UnregisteredSource(logger, job.Id, job.Source);
            return;
        }

        using var jobScope = logger.BeginJobScope(job.Id, job.Source, _workerId);

        ImportLog.JobStarted(logger, job.Id, job.Source, job.Scope, job.AttemptCount);

        // Cancellation is scoped to this execution: the process may be shutting down, or the
        // heartbeat may observe that the job was cancelled or taken over.
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var progress = new ImportProgressSink(
            jobs,
            job.Id,
            _workerId,
            Options.Create(_engine),
            logger);

        var context = new ImportExecutionContext(
            job.Id,
            job.BatchSize,
            job.MaxParallelism,
            progress);

        var heartbeat = HeartbeatAsync(job.Id, progress, executionCts, stoppingToken);

        try
        {
            await source.ExecuteAsync(context, executionCts.Token);
            await progress.FlushAsync(CancellationToken.None);

            var finished = await jobs.GetAsync(job.Id, CancellationToken.None);
            var status = finished is { TotalFailedRecords: > 0 }
                ? ImportJobStatus.CompletedWithErrors
                : ImportJobStatus.Completed;

            await jobs.CompleteAsync(
                job.Id,
                _workerId,
                status,
                error: null,
                CancellationToken.None);

            if (status == ImportJobStatus.CompletedWithErrors)
            {
                ImportLog.JobCompletedWithErrors(logger, job.Id, finished?.TotalProcessedRecords, finished?.TotalFailedRecords);
            }
            else
            {
                ImportLog.JobCompleted(logger, job.Id, finished?.TotalProcessedRecords, finished?.TotalFailedRecords);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await jobs.ReleaseAsync(
                job.Id, _workerId, "Application is shutting down.", CancellationToken.None);

            ImportLog.JobReleased(logger, job.Id);
        }
        catch (OperationCanceledException)
        {
            // The job was cancelled or lost its lease; the row is already in its final state.
            ImportLog.JobStopped(logger, job.Id);
        }
        catch (Exception exception)
        {
            var retryPolicy = scope.ServiceProvider.GetRequiredService<ImportRetryPolicy>();

            await HandleFailureAsync(jobs, retryPolicy, job, exception);
        }
        finally
        {
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

                var renewed = await RenewAsync(jobId, stoppingToken);

                if (renewed)
                    continue;

                WorkerLog.WorkerLeaseLost(logger, jobId, _workerId);

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

    private async Task<bool> RenewAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

            return await jobs.RenewLeaseAsync(
                jobId,
                _workerId,
                TimeSpan.FromSeconds(_engine.DefaultLeaseDurationSeconds),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerLeaseRenewFailed(logger, jobId, exception);
            return true;
        }
    }

    private async Task HandleFailureAsync(
        IImportJobRepository jobs,
        ImportRetryPolicy retryPolicy,
        ImportJob job,
        Exception exception)
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
            _workerId,
            exception.Message,
            retryAt,
            CancellationToken.None);
    }
}
