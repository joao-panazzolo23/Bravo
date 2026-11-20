namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportFailedRecordViewModel(
    Guid Id,
    string SourceIdentifier,
    string? Payload,
    string ErrorCategory,
    string ErrorDescription,
    int AttemptCount,
    DateTime CreatedAt
);
