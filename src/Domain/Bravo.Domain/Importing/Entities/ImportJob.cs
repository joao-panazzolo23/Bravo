using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

/// <summary>
/// Durable record of one import run. Execution state lives here, at job level: there is no
/// per-work-unit persistence, so an interrupted job is simply executed again from the start.
/// </summary>
public class ImportJob : Entity
{
    public string Source { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public ImportJobStatus Status { get; private set; }
    public int BatchSize { get; private set; }
    public int MaxAttempts { get; private set; }
    public int MaxParallelism { get; private set; }
    public int LeaseDurationSeconds { get; private set; }

    /// <summary>Number of times a worker has claimed this job.</summary>
    public int AttemptCount { get; private set; }

    public ImportStage Stage { get; private set; }
    public int TotalOperations { get; private set; }
    public int CompletedOperations { get; private set; }
    public int FailedOperations { get; private set; }
    public long TotalProcessedRecords { get; private set; }
    public long TotalFailedRecords { get; private set; }
    public string? CurrentOperation { get; private set; }
    public string? LastError { get; private set; }

    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ProgressUpdatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public bool IsTerminal =>
        Status is ImportJobStatus.Completed
        or ImportJobStatus.CompletedWithErrors
        or ImportJobStatus.Failed
        or ImportJobStatus.Cancelled;

    private ImportJob() { }

    private ImportJob(
        string source,
        string scope,
        int batchSize,
        int maxAttempts,
        int maxParallelism,
        int leaseDurationSeconds)
    {
        Source = source;
        Scope = scope;
        Status = ImportJobStatus.Pending;
        BatchSize = batchSize;
        MaxAttempts = maxAttempts;
        MaxParallelism = maxParallelism;
        LeaseDurationSeconds = leaseDurationSeconds;
    }

    public static ImportJob Create(
        string source,
        string scope,
        int batchSize,
        int maxAttempts,
        int maxParallelism,
        int leaseDurationSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leaseDurationSeconds);

        var now = DateTime.UtcNow;
        var job = new ImportJob(
            source,
            scope,
            batchSize,
            maxAttempts,
            maxParallelism,
            leaseDurationSeconds);

        job.SetCreatedAt(now);
        job.SetUpdatedAt(now);

        return job;
    }
}
