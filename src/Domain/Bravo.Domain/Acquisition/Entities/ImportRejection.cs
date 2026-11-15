using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

/// <summary>
/// A bounded record of a rejected source record. Only a small sample is persisted so that
/// malformed datasets remain diagnosable without storing entire files in the database.
/// </summary>
public class ImportRejection : Entity
{
    public Guid ExecutionId { get; private set; }
    public ImportExecution Execution { get; private set; } = null!;
    public string DatasetKey { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? RecordReference { get; private set; }
    public string? PayloadSample { get; private set; }

    private ImportRejection() { }

    public static ImportRejection Create(
        Guid executionId,
        string datasetKey,
        string reason,
        string? recordReference,
        string? payloadSample)
    {
        var rejection = new ImportRejection
        {
            ExecutionId = executionId,
            DatasetKey = Truncate(datasetKey, 150),
            Reason = Truncate(reason, 500),
            RecordReference = Truncate(recordReference, 500),
            PayloadSample = Truncate(payloadSample, 2000)
        };
        rejection.SetCreatedAt(DateTime.UtcNow);
        rejection.SetUpdatedAt(DateTime.UtcNow);
        return rejection;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
