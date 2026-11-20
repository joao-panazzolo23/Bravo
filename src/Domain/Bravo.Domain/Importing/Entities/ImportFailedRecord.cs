using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

/// <summary>
/// A source record that could not be imported. Uniqueness is (job, source identifier), so a
/// re-executed import updates the same dead letter instead of duplicating it.
/// </summary>
public class ImportFailedRecord : Entity
{
    public Guid JobId { get; private set; }
    public string SourceIdentifier { get; private set; } = string.Empty;
    public string? Payload { get; private set; }
    public ImportErrorCategory ErrorCategory { get; private set; }
    public string ErrorDescription { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }

    private ImportFailedRecord() { }

    public static ImportFailedRecord Create(
        Guid jobId,
        string sourceIdentifier,
        string? payload,
        ImportErrorCategory errorCategory,
        string errorDescription,
        int attemptCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorDescription);

        var now = DateTime.UtcNow;
        var record = new ImportFailedRecord
        {
            JobId = jobId,
            SourceIdentifier = sourceIdentifier,
            Payload = payload,
            ErrorCategory = errorCategory,
            ErrorDescription = errorDescription,
            AttemptCount = attemptCount
        };

        record.SetCreatedAt(now);
        record.SetUpdatedAt(now);
        return record;
    }
}
