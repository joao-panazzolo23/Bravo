using Bravo.Domain.Importing.Enums;

namespace Bravo.Application.Importing.Models;

public sealed record ImportRecordValidationResult(
    bool IsValid,
    string? Error,
    ImportErrorCategory Category)
{
    public static ImportRecordValidationResult Valid()
        => new(true, null, ImportErrorCategory.Validation);

    public static ImportRecordValidationResult Invalid(
        string error,
        ImportErrorCategory category = ImportErrorCategory.Validation)
        => new(false, error, category);
}
