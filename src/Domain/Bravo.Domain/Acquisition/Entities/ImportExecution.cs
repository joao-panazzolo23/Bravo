using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

/// <summary>
/// Persistent state of one import execution. PostgreSQL is the authoritative source of this
/// state: the in-process queue only dispatches executions that are already persisted here.
/// </summary>
public class ImportExecution : Entity
{
    public ImportOperationType OperationType { get; private set; }
    public string? Scope { get; private set; }
    public ImportStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CurrentStage { get; private set; }
    public int RecordsProcessed { get; private set; }
    public int RecordsPersisted { get; private set; }
    public int RecordsRejected { get; private set; }
    public int RecordsSkipped { get; private set; }
    public int BatchesCommitted { get; private set; }
    public long BytesDownloaded { get; private set; }
    public int RecoveryAttempts { get; private set; }
    public DateTime? HeartbeatAt { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public string? FailureDetails { get; private set; }
    public string? CancellationReason { get; private set; }

    public ICollection<ImportCheckpoint> Checkpoints { get; private set; } = [];
    public ICollection<DatasetProvenance> Provenances { get; private set; } = [];
    public ICollection<ImportRejection> Rejections { get; private set; } = [];

    private ImportExecution() { }

    public static ImportExecution Create(ImportOperationType operationType, string? scope = null)
    {
        var execution = new ImportExecution
        {
            OperationType = operationType,
            Scope = scope,
            Status = ImportStatus.Pending
        };
        execution.SetCreatedAt(DateTime.UtcNow);
        execution.SetUpdatedAt(DateTime.UtcNow);
        return execution;
    }

    public void MarkRunning()
    {
        Status = ImportStatus.Running;
        StartedAt ??= DateTime.UtcNow;
        HeartbeatAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkResumed()
    {
        RecoveryAttempts++;
        Status = ImportStatus.Running;
        HeartbeatAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkCompleted()
    {
        Status = RecordsRejected > 0 ? ImportStatus.CompletedWithErrors : ImportStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkFailed(string details)
    {
        Status = ImportStatus.Failed;
        FailureDetails = Truncate(details, 4000);
        CompletedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkCancelled(string reason)
    {
        Status = ImportStatus.Cancelled;
        CancellationReason = Truncate(reason, 500);
        CompletedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void RequestCancellation()
    {
        Status = ImportStatus.CancellationRequested;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void UpdateProgress(string stage, int processed, int persisted)
    {
        CurrentStage = stage;
        RecordsProcessed = processed;
        RecordsPersisted = persisted;
        HeartbeatAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void RecordCounters(
        int processed,
        int persisted,
        int rejected,
        int skipped,
        int batchesCommitted,
        long bytesDownloaded,
        string? stage = null)
    {
        RecordsProcessed = processed;
        RecordsPersisted = persisted;
        RecordsRejected = rejected;
        RecordsSkipped = skipped;
        BatchesCommitted = batchesCommitted;
        BytesDownloaded = bytesDownloaded;
        if (stage is not null)
        {
            CurrentStage = stage;
        }

        HeartbeatAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void RegisterDownloadedBytes(long bytes)
    {
        BytesDownloaded += bytes;
        SetUpdatedAt(DateTime.UtcNow);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
