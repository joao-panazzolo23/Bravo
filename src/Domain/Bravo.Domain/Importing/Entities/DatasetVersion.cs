using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Importing.Entities;

public class DatasetVersion : Entity
{
    public Guid ImportDatasetId { get; private set; }
    public string Scope { get; private set; } = string.Empty;
    public DatasetVersionStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? ImportJobId { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? SupersededAt { get; private set; }

    private DatasetVersion() { }

    public static DatasetVersion Create(Guid importDatasetId, string scope, Guid? importJobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTime.UtcNow;
        var version = new DatasetVersion
        {
            ImportDatasetId = importDatasetId,
            Scope = scope,
            Status = DatasetVersionStatus.Pending,
            ImportJobId = importJobId
        };

        version.SetCreatedAt(now);
        version.SetUpdatedAt(now);

        return version;
    }
}
