using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Models;

namespace Bravo.Infrastructure.Acquisition.Validators;

//JESUS, that`s horrible, this should be in application
public sealed class MunicipalityValidator : IImportRecordValidator<IbgeMunicipalityRecord>
{
    public ImportRecordValidationResult Validate(IbgeMunicipalityRecord record)
    {
        if (record.Code <= 0)
            return ImportRecordValidationResult.Invalid("The IBGE code must be positive.");

        if (record.StateCode <= 0)
            return ImportRecordValidationResult.Invalid("The state IBGE code must be positive.");

        if (string.IsNullOrWhiteSpace(record.Name) || record.Name.Length > 150)
            return ImportRecordValidationResult.Invalid("The municipality name must have between 1 and 150 characters.");

        return ImportRecordValidationResult.Valid();
    }
}
