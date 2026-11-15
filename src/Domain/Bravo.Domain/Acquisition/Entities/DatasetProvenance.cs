using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

/// <summary>
/// Provenance of one acquired dataset: where it came from, how it was validated and which
/// execution produced it. It is appended per acquisition so that historical imports remain
/// traceable even after a dataset is replaced at the source.
/// </summary>
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
    public string? ETag { get; private set; }
    public DateTime? SourceLastModifiedAt { get; private set; }
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
            Provider = Truncate(provider, 50),
            DatasetIdentifier = Truncate(datasetIdentifier, 200),
            SourceUrl = Truncate(sourceUrl, 1000),
            Version = Truncate(version, 50),
            FormatDescription = Truncate(formatDescription, 50),
            OriginalFilename = Truncate(originalFilename, 250),
            FetchedAt = DateTime.UtcNow
        };
        provenance.SetCreatedAt(DateTime.UtcNow);
        provenance.SetUpdatedAt(DateTime.UtcNow);
        return provenance;
    }

    public void SetContentHash(string hash) => ContentHash = Truncate(hash, 128);
    public void SetFileSizeBytes(long size) => FileSizeBytes = size;
    public void SetDataDate(DateTime date) => DataDate = date;
    public void SetETag(string? etag) => ETag = Truncate(etag, 200);
    public void SetSourceLastModifiedAt(DateTime? lastModified) => SourceLastModifiedAt = lastModified;

    /// <summary>
    /// Source metadata can exceed the configured column lengths (for example a long original
    /// filename). Provenance must never fail an import because of a descriptive field, so the
    /// values are bounded here rather than at the persistence layer.
    /// </summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
