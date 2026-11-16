using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

public class ImportDataset : Entity
{
    public string Provider { get; private set; } = string.Empty;
    public string Dataset { get; private set; } = string.Empty;
    public int? ElectionYear { get; private set; }
    public string LayoutVersion { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string SourceFileName { get; private set; } = string.Empty;
    public string Checksum { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime AcquiredAt { get; private set; }

    private ImportDataset() { }

    public static ImportDataset Create(
        string provider,
        string dataset,
        int? electionYear,
        string layoutVersion,
        string sourceUrl,
        string sourceFileName,
        string checksum,
        long sizeBytes,
        DateTime acquiredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);

        var record = new ImportDataset
        {
            Provider = provider,
            Dataset = dataset,
            ElectionYear = electionYear,
            LayoutVersion = layoutVersion,
            SourceUrl = sourceUrl,
            SourceFileName = sourceFileName,
            Checksum = checksum,
            SizeBytes = sizeBytes,
            AcquiredAt = acquiredAt
        };

        record.SetCreatedAt(acquiredAt);
        record.SetUpdatedAt(acquiredAt);

        return record;
    }
}
