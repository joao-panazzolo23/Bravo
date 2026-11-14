using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

public class DatasetProvenance : Entity
{
    public Guid ExecutionId { get; private set; }
    public ImportExecution Execution { get; private set; } = null!;
    public string Provider { get; private set; } = string.Empty;
    public string DatasetIdentifier { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string? OriginalFilename { get; private set; }
    public DateTime? DataDate { get; private set; }
    public string? ContentHash { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? FormatDescription { get; private set; }
    public string? Version { get; private set; }
    public DateTime FetchedAt { get; private set; }

    private DatasetProvenance() { }

    public static DatasetProvenance Create(
        Guid executionId,
        string provider,
        string datasetIdentifier,
        string sourceUrl,
        string? version = null,
        string? formatDescription = null,
        string? originalFilename = null)
    {
        var provenance = new DatasetProvenance
        {
            ExecutionId = executionId,
            Provider = provider,
            DatasetIdentifier = datasetIdentifier,
            SourceUrl = sourceUrl,
            Version = version,
            FormatDescription = formatDescription,
            OriginalFilename = originalFilename,
            FetchedAt = DateTime.UtcNow
        };
        provenance.SetCreatedAt(DateTime.UtcNow);
        provenance.SetUpdatedAt(DateTime.UtcNow);
        return provenance;
    }

    public void SetContentHash(string hash) => ContentHash = hash;
    public void SetFileSizeBytes(long size) => FileSizeBytes = size;
    public void SetDataDate(DateTime date) => DataDate = date;
}
