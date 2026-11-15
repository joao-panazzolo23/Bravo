using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

public class ImportWorkUnit : Entity
{
    public Guid JobId { get; private set; }
    public string UnitKey { get; private set; } = string.Empty;
    public ImportWorkUnitStatus Status { get; private set; }
    public string? Cursor { get; private set; }
    public long ProcessedRecords { get; private set; }
    public long FailedRecords { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public long FencingToken { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private ImportWorkUnit() { }

    public static ImportWorkUnit Create(Guid jobId, string unitKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitKey);

        var now = DateTime.UtcNow;
        var unit = new ImportWorkUnit
        {
            JobId = jobId,
            UnitKey = unitKey,
            Status = ImportWorkUnitStatus.Pending
        };

        unit.SetCreatedAt(now);
        unit.SetUpdatedAt(now);
        return unit;
    }

    public void AdvanceCursor(string? nextCursor, int processedRecords, int failedRecords)
    {
        Cursor = nextCursor;
        ProcessedRecords += processedRecords;
        FailedRecords += failedRecords;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
