using Bravo.Domain.Acquisition.Entities;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Shared.Data;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Acquisition.Services;

/// <summary>
/// Shared lifecycle handling for import handlers: opening the execution, writing bounded
/// progress updates, saving checkpoints only after committed work, and recording the final
/// state. It deliberately contains orchestration only; persistence stays in the injected
/// services.
/// </summary>
public sealed class ImportExecutionRunner(
    IImportExecutionRepository executionRepository,
    IImportCheckpointRepository checkpointRepository,
    IImportRejectionRepository rejectionRepository,
    IUnityOfWork unityOfWork,
    ILogger<ImportExecutionRunner> logger)
{
    private static readonly TimeSpan MinimumProgressInterval = TimeSpan.FromSeconds(2);
    private DateTime _lastProgressWrite = DateTime.MinValue;

    public async Task<ImportExecution?> OpenAsync(Guid executionId, CancellationToken ct)
    {
        // Opening the execution is administrative work on the execution row itself. It must
        // complete even when the caller has already requested cancellation, otherwise the
        // execution would stay Pending forever. Pipeline work checks cancellation afterwards.
        var execution = await executionRepository.GetById(executionId, CancellationToken.None);
        if (execution is null)
        {
            logger.LogError("Execution {ExecutionId} not found.", executionId);
            return null;
        }

        if (execution.Status == ImportStatus.CancellationRequested)
        {
            execution.MarkCancelled("Cancellation requested prior to execution start.");
            await executionRepository.Update(execution, CancellationToken.None);
            await unityOfWork.Commit();
            return null;
        }

        if (execution.Status is ImportStatus.Running)
        {
            execution.MarkResumed();
        }
        else
        {
            execution.MarkRunning();
        }

        await executionRepository.Update(execution, CancellationToken.None);
        await unityOfWork.Commit();

        ct.ThrowIfCancellationRequested();
        return execution;
    }

    public async Task<IReadOnlySet<string>> GetCompletedKeysAsync(ImportExecution execution, CancellationToken ct)
    {
        return await checkpointRepository.GetCompletedKeys(execution.Id, ct);
    }

    public async Task SetStageAsync(ImportExecution execution, string stage, CancellationToken ct)
    {
        execution.UpdateProgress(stage, execution.RecordsProcessed, execution.RecordsPersisted);
        await executionRepository.Update(execution, ct);
        await unityOfWork.Commit();
    }

    /// <summary>
    /// Writes progress using a minimum interval so that progress reporting does not become a
    /// per-record database write.
    /// </summary>
    public async Task ReportProgressAsync(
        ImportExecution execution,
        string stage,
        Counters counters,
        CancellationToken ct,
        bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - _lastProgressWrite < MinimumProgressInterval)
        {
            return;
        }

        _lastProgressWrite = now;
        execution.RecordCounters(
            counters.Processed,
            counters.Persisted,
            counters.Rejected,
            counters.Skipped,
            counters.Batches,
            counters.BytesDownloaded,
            stage);

        await executionRepository.Update(execution, ct);
        await unityOfWork.Commit();
    }

    public async Task CheckpointAsync(
        ImportExecution execution,
        string datasetKey,
        string data,
        int recordsCompleted,
        Counters counters,
        CancellationToken ct)
    {
        await checkpointRepository.Save(
            ImportCheckpoint.Create(execution.Id, datasetKey, data, recordsCompleted),
            ct);

        execution.RecordCounters(
            counters.Processed,
            counters.Persisted,
            counters.Rejected,
            counters.Skipped,
            counters.Batches,
            counters.BytesDownloaded);

        await executionRepository.Update(execution, ct);
        await unityOfWork.Commit();
    }

    public async Task RecordRejectionsAsync(
        Guid executionId,
        string datasetKey,
        IReadOnlyList<RejectedRecord> rejections,
        CancellationToken ct)
    {
        if (rejections.Count == 0)
        {
            return;
        }

        await rejectionRepository.AddRange(
            rejections.Select(r => ImportRejection.Create(
                executionId,
                datasetKey,
                r.Reason,
                r.Reference,
                r.PayloadSample))
                .ToList(),
            ct);
    }

    public async Task CompleteAsync(ImportExecution execution, Counters counters, CancellationToken ct)
    {
        execution.RecordCounters(
            counters.Processed,
            counters.Persisted,
            counters.Rejected,
            counters.Skipped,
            counters.Batches,
            counters.BytesDownloaded,
            "Completed");

        execution.MarkCompleted();
        await executionRepository.Update(execution, ct);
        await unityOfWork.Commit();
    }

    public async Task CancelledAsync(ImportExecution execution, string reason)
    {
        execution.MarkCancelled(reason);
        await executionRepository.Update(execution, CancellationToken.None);
        await unityOfWork.Commit();
    }

    public async Task FailedAsync(ImportExecution execution, string reason)
    {
        execution.MarkFailed(reason);
        await executionRepository.Update(execution, CancellationToken.None);
        await unityOfWork.Commit();
    }
}

/// <summary>
/// Mutable counters for one execution. Progress is written from this instance.
/// </summary>
public sealed class Counters
{
    public int Processed { get; set; }
    public int Persisted { get; set; }
    public int Rejected { get; set; }
    public int Skipped { get; set; }
    public int Batches { get; set; }
    public long BytesDownloaded { get; set; }
}

public record RejectedRecord(string Reason, string? Reference, string? PayloadSample);
