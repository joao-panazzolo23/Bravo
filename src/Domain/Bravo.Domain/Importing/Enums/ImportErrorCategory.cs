namespace Bravo.Domain.Importing.Enums;

public enum ImportErrorCategory
{
    Validation,
    Deserialization,
    Source,
    Persistence,
    Infrastructure
}
