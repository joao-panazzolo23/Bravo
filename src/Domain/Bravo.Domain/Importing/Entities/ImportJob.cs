using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

public class ImportJob : Entity
{
    public string ImportType { get; private set; } = string.Empty;
    public ImportJobStatus Status { get; private set; }
    public ImportConflictStrategy ConflictStrategy { get; private set; }
    public int BatchSize { get; private set; }
    public int MaxAttempts { get; private set; }
    public int LeaseDurationSeconds { get; private set; }
    public long TotalProcessedRecords { get; private set; }
    public long TotalFailedRecords { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public bool IsTerminal => Status is ImportJobStatus.Completed
        or ImportJobStatus.CompletedWithErrors
        or ImportJobStatus.Failed
        or ImportJobStatus.Cancelled;

    private ImportJob() { }

    public static ImportJob Create(
        string importType,
        ImportConflictStrategy conflictStrategy,
        int batchSize,
        int maxAttempts,
        int leaseDurationSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDurationSeconds);

        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            ImportType = importType,
            Status = ImportJobStatus.Pending,
            ConflictStrategy = conflictStrategy,
            BatchSize = batchSize,
            MaxAttempts = maxAttempts,
            LeaseDurationSeconds = leaseDurationSeconds
        };

        job.SetCreatedAt(now);
        job.SetUpdatedAt(now);
        return job;
    }
}
