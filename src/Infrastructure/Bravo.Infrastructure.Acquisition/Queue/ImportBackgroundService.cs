using Bravo.Application.Acquisition.Handlers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Acquisition.Queue;

/// <summary>
/// Consumes persisted import executions. PostgreSQL remains the source of truth: at startup
/// the service re-dispatches every execution that is still pending or whose lease expired,
/// and each execution is claimed with a database lease before it starts so two API instances
/// cannot run the same import simultaneously.
/// </summary>
public sealed class ImportBackgroundService(
    ImportExecutionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportBackgroundService> logger
) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CancellationPollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingExecutionsAsync(stoppingToken);

        await foreach (var executionId in queue.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;

            using var executionCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, executionCts.Token);
            queue.RegisterActive(executionId, executionCts);

            try
            {
                await RunExecutionAsync(executionId, linkedCts.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error processing import execution {ExecutionId}", executionId);
            }
            finally
            {
                queue.UnregisterActive(executionId);
            }
        }
    }

    private async Task RunExecutionAsync(Guid executionId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var executionRepo = scope.ServiceProvider.GetRequiredService<IImportExecutionRepository>();

        if (!await executionRepo.TryClaim(executionId, LeaseDuration, ct))
        {
            logger.LogInformation(
                "Import execution {ExecutionId} is already claimed by another process or has finished.",
                executionId);
            return;
        }

        var execution = await executionRepo.GetById(executionId, ct);
        if (execution is null)
        {
            logger.LogWarning("Import execution {ExecutionId} was not found in the repository.", executionId);
            return;
        }

        if (execution.Status is ImportStatus.CancellationRequested)
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnityOfWork>();
            execution.MarkCancelled("Cancellation requested prior to start.");
            await executionRepo.Update(execution, CancellationToken.None);
            await uow.Commit();
            return;
        }

        using var cancellationWatcher = new CancellationWatcher(
            scopeFactory,
            executionId,
            ct,
            logger);

        cancellationWatcher.Start();

        switch (execution.OperationType)
        {
            case ImportOperationType.IbgeGeography:
                await scope.ServiceProvider.GetRequiredService<IbgeImportHandler>()
                    .ExecuteAsync(executionId, cancellationWatcher.Token);
                break;

            case ImportOperationType.FgvMunicipalityCrosswalk:
                await scope.ServiceProvider.GetRequiredService<FgvCrosswalkImportHandler>()
                    .ExecuteAsync(executionId, cancellationWatcher.Token);
                break;

            case ImportOperationType.TseCandidates:
            case ImportOperationType.TseElectionMetadata:
            {
                var parsed = ImportScope.Parse(execution.OperationType, execution.Scope);
                if (parsed.Data is null || parsed.StatusCode >= 400)
                {
                    await FailAsync(executionRepo, execution, parsed.Message ?? "Invalid import scope.", ct);
                    return;
                }

                await scope.ServiceProvider.GetRequiredService<TseCandidatesImportHandler>()
                    .ExecuteAsync(executionId, parsed.Data, cancellationWatcher.Token);
                break;
            }

            case ImportOperationType.TseResults:
            {
                var parsed = ImportScope.Parse(execution.OperationType, execution.Scope);
                if (parsed.Data is null || parsed.StatusCode >= 400)
                {
                    await FailAsync(executionRepo, execution, parsed.Message ?? "Invalid import scope.", ct);
                    return;
                }

                await scope.ServiceProvider.GetRequiredService<TseResultsImportHandler>()
                    .ExecuteAsync(executionId, parsed.Data, cancellationWatcher.Token);
                break;
            }

            default:
                await FailAsync(executionRepo, execution, $"Unsupported operation type {execution.OperationType}.", ct);
                break;
        }

        cancellationWatcher.Stop();
    }

    private static async Task FailAsync(
        IImportExecutionRepository repository,
        Domain.Acquisition.Entities.ImportExecution execution,
        string reason,
        CancellationToken ct)
    {
        execution.MarkFailed(reason);
        await repository.Update(execution, CancellationToken.None);
    }

    /// <summary>
    /// Re-dispatches persisted executions after a restart. Running executions whose lease is
    /// still valid belong to a live instance and are not touched; abandoned ones are claimed
    /// again through the lease and resume from their last committed checkpoint.
    /// </summary>
    private async Task RecoverPendingExecutionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IImportExecutionRepository>();

            var ids = await repo.GetDispatchableIds(cancellationToken);
            foreach (var id in ids)
            {
                logger.LogInformation("Re-dispatching import execution {ExecutionId} after startup.", id);
                await queue.Enqueue(id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending or interrupted import executions on startup.");
        }
    }

    /// <summary>
    /// Observes the persisted status of a running execution so a cancellation requested
    /// through the API stops the pipeline even though the request is handled in another scope.
    /// </summary>
    private sealed class CancellationWatcher : IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Guid _executionId;
        private readonly CancellationToken _parent;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts;
        private Task? _loop;

        public CancellationWatcher(
            IServiceScopeFactory scopeFactory,
            Guid executionId,
            CancellationToken parent,
            ILogger logger)
        {
            _scopeFactory = scopeFactory;
            _executionId = executionId;
            _parent = parent;
            _logger = logger;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
        }

        public CancellationToken Token => _cts.Token;

        public void Start()
        {
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(CancellationPollInterval, _cts.Token);
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IImportExecutionRepository>();
                        var status = await repo.GetStatus(_executionId, _cts.Token);
                        if (status is ImportStatus.CancellationRequested or ImportStatus.Cancelled)
                        {
                            _logger.LogInformation("Cancellation observed for import execution {ExecutionId}.", _executionId);
                            _cts.Cancel();
                            return;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not poll cancellation state for execution {ExecutionId}.", _executionId);
                    }
                }
            }, CancellationToken.None);
        }

        public void Stop()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}
