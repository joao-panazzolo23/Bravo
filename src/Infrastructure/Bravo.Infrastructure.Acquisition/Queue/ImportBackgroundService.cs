using Bravo.Application.Acquisition.Handlers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Acquisition.Queue;

public sealed class ImportBackgroundService(
    ImportExecutionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportBackgroundService> logger
) : BackgroundService
{
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var executionRepo = scope.ServiceProvider.GetRequiredService<IImportExecutionRepository>();
                var execution = await executionRepo.GetById(executionId, linkedCts.Token);

                if (execution is null)
                {
                    logger.LogWarning("Import execution {ExecutionId} was not found in repository.", executionId);
                    continue;
                }

                if (execution.Status is ImportStatus.CancellationRequested)
                {
                    var uow = scope.ServiceProvider.GetRequiredService<IUnityOfWork>();
                    execution.MarkCancelled("Cancellation requested prior to start.");
                    await executionRepo.Update(execution, CancellationToken.None);
                    await uow.Commit();
                    continue;
                }

                switch (execution.OperationType)
                {
                    case ImportOperationType.IbgeGeography:
                        var ibgeHandler = scope.ServiceProvider.GetRequiredService<IbgeImportHandler>();
                        await ibgeHandler.ExecuteAsync(executionId, linkedCts.Token);
                        break;

                    default:
                        logger.LogError("Unsupported operation type {OperationType} for execution {ExecutionId}", execution.OperationType, executionId);
                        break;
                }
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

    private async Task RecoverPendingExecutionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IImportExecutionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnityOfWork>();

            var running = await repo.GetByStatus(ImportStatus.Running, cancellationToken);
            foreach (var item in running)
            {
                item.MarkFailed("Execution was interrupted by process restart. Can be safely re-triggered with checkpoints.");
                await repo.Update(item, cancellationToken);
            }

            if (running.Count > 0)
            {
                await uow.Commit();
            }

            var pending = await repo.GetByStatus(ImportStatus.Pending, cancellationToken);
            foreach (var item in pending)
            {
                await queue.Enqueue(item.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending or interrupted import executions on startup.");
        }
    }
}

