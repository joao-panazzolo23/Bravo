using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Geography.Entities;

namespace Bravo.Infrastructure.Acquisition.Validators;

public sealed class StateValidator : IImportRecordValidator<State>
{
    public ImportRecordValidationResult Validate(State record)
    {
        if (record.IbgeCode <= 0)
            return ImportRecordValidationResult.Invalid("The IBGE code must be positive.");

        if (record.Abbreviation.Length != 2)
            return ImportRecordValidationResult.Invalid("The state abbreviation must have two characters.");

        if (string.IsNullOrWhiteSpace(record.Name) || record.Name.Length > 100)
            return ImportRecordValidationResult.Invalid("The state name must have between 1 and 100 characters.");

        if (string.IsNullOrWhiteSpace(record.Region) || record.Region.Length > 20)
            return ImportRecordValidationResult.Invalid("The state region must have between 1 and 20 characters.");

        return ImportRecordValidationResult.Valid();
    }
}
