namespace Bravo.Application.Imports.Common.ViewModels;

public sealed record ImportJobStatusViewModel(
    Guid Id,
    string Source,
    string Scope,
    string Status,
    string Stage,
    int BatchSize,
    int MaxAttempts,
    int MaxParallelism,
    int LeaseDurationSeconds,
    int AttemptCount,
    int TotalOperations,
    int CompletedOperations,
    int FailedOperations,
    long TotalProcessedRecords,
    long TotalFailedRecords,
    string? CurrentOperation,
    string? LastError,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt
);
