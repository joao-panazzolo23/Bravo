using Bravo.Domain.Importing.Models;

namespace Bravo.Domain.Importing.Services;

public interface IImportRecordValidator<in TRecord>
{
    ImportRecordValidationResult Validate(TRecord record);
}
