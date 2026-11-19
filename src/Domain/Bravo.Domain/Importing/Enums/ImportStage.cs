namespace Bravo.Domain.Importing.Enums;

/// <summary>
/// Observable stages a work unit moves through while a pipeline executes it.
/// </summary>
/// <remarks>
/// Values are persisted as text via <c>ImportWorkUnits.CurrentStage</c>, so existing rows are
/// readable and adding a stage does not require a data migration of numeric values.
/// </remarks>
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
    Completed,
    Failed
}
