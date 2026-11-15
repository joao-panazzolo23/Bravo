namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportJobStatusViewModel(
    Guid Id,
    string Status,
    int BatchSize,
    int MaxAttempts,
    int LeaseDurationSeconds,
    long TotalProcessedRecords,
    long TotalFailedRecords,
    string? LastError,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ImportWorkUnitStatusViewModel> WorkUnits);
