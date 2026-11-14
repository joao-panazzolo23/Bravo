using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

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
    public string? FailureDetails { get; private set; }
    public string? CancellationReason { get; private set; }

    public ICollection<ImportCheckpoint> Checkpoints { get; private set; } = [];
    public ICollection<DatasetProvenance> Provenances { get; private set; } = [];

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
        StartedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkCompleted()
    {
        Status = ImportStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkFailed(string details)
    {
        Status = ImportStatus.Failed;
        FailureDetails = details;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkCancelled(string reason)
    {
        Status = ImportStatus.Cancelled;
        CancellationReason = reason;
        CompletedAt = DateTime.UtcNow;
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
        SetUpdatedAt(DateTime.UtcNow);
    }
}
