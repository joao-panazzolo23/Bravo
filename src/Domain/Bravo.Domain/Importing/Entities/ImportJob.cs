using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

public class ImportJob : Entity
{
    public ImportJobStatus Status { get; private set; }
    public int BatchSize { get; private set; }
    public int MaxAttempts { get; private set; }
    public int LeaseDurationSeconds { get; private set; }
    public long TotalProcessedRecords { get; private set; }
    public long TotalFailedRecords { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public bool IsTerminal =>
        Status is ImportJobStatus.Completed
        or ImportJobStatus.CompletedWithErrors
        or ImportJobStatus.Failed
        or ImportJobStatus.Cancelled;

    private ImportJob() { }

    private ImportJob(
        int batchSize,
        int maxAttempts,
        int leaseDurationSeconds
    )
    {
        Status = ImportJobStatus.Pending;
        BatchSize = batchSize;
        MaxAttempts = maxAttempts;
        LeaseDurationSeconds = leaseDurationSeconds;
    }

    public static ImportJob Create(
        int batchSize,
        int maxAttempts,
        int leaseDurationSeconds
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDurationSeconds);

        return new ImportJob(
            batchSize,
            maxAttempts,
            leaseDurationSeconds
        );
    }
}
