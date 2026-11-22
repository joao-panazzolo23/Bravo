using System.Collections.Concurrent;
using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Logging;
using Mediator;
using Microsoft.Extensions.Options;

namespace Bravo.Presentation.Workers;

/// <summary>
/// Background scheduler for durable import jobs.
/// </summary>
/// <remarks>
/// This type owns scheduling only: poll for eligible jobs, cap how many run at once, dispatch
/// each one, track the executions and stop cleanly. Claiming, lease renewal, progress, retry
/// classification and source execution belong to the application layer, reached through
/// <see cref="ExecuteImportJobCommand"/>.
///
/// Implemented as a <see cref="BackgroundService"/> and therefore a singleton: every dispatch
/// resolves a fresh scope, so no scoped service (repository, executor, source) is retained
/// between jobs.
/// </remarks>
public sealed class ImportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ImportWorkerOptions> workerOptions,
    ILogger<ImportWorker> logger) : BackgroundService
{
    private readonly ImportWorkerOptions _options = workerOptions.Value;
    private readonly ConcurrentDictionary<Guid, Task> _active = new();
    private readonly SemaphoreSlim _slots = new(workerOptions.Value.MaxConcurrentJobs);
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            WorkerLog.WorkerDisabled(logger);
            return;
        }

        if (_options.RecoverOnStartup)
            await RecoverAsync(stoppingToken);

        WorkerLog.WorkerStarted(
            logger, _workerId, _options.PollIntervalSeconds, _options.MaxConcurrentJobs);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

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

        WorkerLog.WorkerStopped(logger, _workerId);
    }

    /// <summary>
    /// Waits for every in-flight execution to finish before the host reports a clean shutdown.
    /// Each execution releases its own job row, so nothing is left ownerless.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        var pending = _active.Values.ToArray();

        if (pending.Length > 0)
            await Task.WhenAll(pending);
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var recovery = scope.ServiceProvider.GetRequiredService<ImportJobRecoveryService>();

        await recovery.RecoverAbandonedAsync(Environment.MachineName, cancellationToken);
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> jobIds;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<IImportJobScheduler>();

            jobIds = await scheduler.ListRecoverableAsync(_options.MaxJobsPerPoll, cancellationToken);
        }

        foreach (var jobId in jobIds)
        {
            if (_active.ContainsKey(jobId))
                continue;

            if (!await _slots.WaitAsync(0, cancellationToken))
            {
                WorkerLog.WorkerIdle(logger, _workerId, _active.Count);
                break;
            }

            // RunJobAsync is the only place that starts and finishes an execution, so the slot
            // is released in exactly one place no matter how the job ends.
            var execution = RunJobAsync(jobId, cancellationToken);

            _active[jobId] = execution;

            _ = execution.ContinueWith(
                _ =>
                {
                    _active.TryRemove(jobId, out _);
                    _slots.Release();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Dispatches one job through the mediator.
    /// </summary>
    /// <remarks>
    /// This method is the scheduler's entire knowledge of job execution. Every failure mode is
    /// handled inside the executor; anything that escapes here is a genuine bug, so it is logged
    /// and observed rather than allowed to become an unobserved task exception.
    /// </remarks>
    private async Task RunJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new ExecuteImportJobCommand(jobId, _workerId),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown; the executor released the job.
        }
        catch (Exception exception)
        {
            // The executor settles expected failures itself, so anything reaching here is a
            // defect. Log it and keep the scheduler alive rather than faulting the host.
            WorkerLog.WorkerDispatchException(logger, jobId, exception);
        }
    }
}
