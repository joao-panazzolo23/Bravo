namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportWorkUnitStatusViewModel(
    Guid Id,
    string UnitKey,
    string Status,
    string? Cursor,
    long ProcessedRecords,
    long FailedRecords,
    int AttemptCount,
    string? LastError,
    string? LeaseOwner,
    DateTime? LeaseExpiresAt,
    long FencingToken,
    DateTime? StartedAt,
    DateTime? CompletedAt);
