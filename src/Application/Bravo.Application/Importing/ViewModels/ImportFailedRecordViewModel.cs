namespace Bravo.Application.Importing.ViewModels;

public sealed record ImportFailedRecordViewModel(
    Guid Id,
    Guid WorkUnitId,
    string SourceIdentifier,
    string? Payload,
    string ErrorCategory,
    string ErrorDescription,
    int AttemptCount,
    DateTime CreatedAt);
