namespace Bravo.Domain.Importing.Enums;

/// <summary>
/// Observable stages an import moves through. Persisted as text on the job row, so adding a
/// stage does not require a data migration of numeric values.
/// </summary>
public enum ImportStage
{
    Pending,
    Initializing,
    Downloading,
    Extracting,
    Reading,
    Parsing,
    Validating,
    Normalizing,
    Persisting,
    Finalizing,
    References,
    Candidates,
    Results,
    Completed,
    Failed
}
