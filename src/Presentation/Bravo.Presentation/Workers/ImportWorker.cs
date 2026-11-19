using System.Collections.Concurrent;
using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Services;
using Microsoft.Extensions.Options;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Presentation.Workers;

public sealed class ImportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ImportWorkerOptions> options,
    ILogger<ImportWorker> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _running = new();
    private readonly SemaphoreSlim _slots = new(options.Value.MaxConcurrentJobs);
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Import worker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));

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
                logger.LogError(exception, "Import worker polling failed.");
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

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        var jobIds = await jobs.ListRecoverableIdsAsync(options.Value.MaxJobsPerPoll, cancellationToken);

        foreach (var jobId in jobIds)
        {
            if (_running.ContainsKey(jobId))
                continue;

            if (!await _slots.WaitAsync(0, cancellationToken))
            {
                logger.LogDebug(
                    "Worker {WorkerId} is at capacity; {RunningJobs} job(s) in flight.",
                    _workerId, _running.Count);
                break;
            }

            logger.LogInformation(
                "Worker {WorkerId} dispatching import job {JobId}. RunningJobs: {RunningJobs}.",
                _workerId, jobId, _running.Count + 1);

            var task = RunJobAsync(jobId, cancellationToken);
            _running[jobId] = task;
            _ = task.ContinueWith(
                completedTask =>
                {
                    _running.TryRemove(jobId, out _);
                    _slots.Release();
                },
                TaskScheduler.Default
                );
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<ImportJobExecutor>();

        try
        {
            await executor.ExecuteAsync(jobId, _workerId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Import worker failed while executing job {JobId}.", jobId);
        }
    }
}
