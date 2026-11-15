using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Contracts;

public interface IImportRecordValidator<in TRecord>
{
    ImportRecordValidationResult Validate(TRecord record);
}
