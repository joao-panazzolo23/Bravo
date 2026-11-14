namespace Bravo.Application.Acquisition.ViewModels;

public record ImportStatusViewModel(
    Guid Id,
    string OperationType,
    string? Scope,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? CurrentStage,
    int RecordsProcessed,
    int RecordsPersisted,
    string? FailureDetails,
    string? CancellationReason
);

