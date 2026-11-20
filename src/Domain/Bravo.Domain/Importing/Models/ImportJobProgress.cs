using Bravo.Domain.Importing.Enums;

namespace Bravo.Domain.Importing.Models;

/// <summary>
/// Absolute, job-level progress snapshot. Values are absolute rather than deltas so a write
/// that races with another one cannot double-count or lose records.
/// </summary>
public sealed record ImportJobProgress(
    ImportStage Stage,
    int TotalOperations,
    int CompletedOperations,
    int FailedOperations,
    long ProcessedRecords,
    long FailedRecords,
    string? CurrentOperation
);
