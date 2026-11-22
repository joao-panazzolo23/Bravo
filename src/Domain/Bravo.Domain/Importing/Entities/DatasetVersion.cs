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

    /// <summary>
    /// Creates a version.
    /// </summary>
    /// <param name="status">
    /// The status the row is inserted with. A caller that immediately starts importing should
    /// pass <see cref="DatasetVersionStatus.Importing"/>, because writing a row as
    /// <see cref="DatasetVersionStatus.Pending"/> only to update it in the next statement is a
    /// second write with no observable intermediate state. The default keeps <c>Pending</c> for
    /// callers that genuinely register a version before starting work.
    /// </param>
    public static DatasetVersion Create(
        Guid importDatasetId,
        string scope,
        Guid? importJobId,
        DatasetVersionStatus status = DatasetVersionStatus.Pending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTime.UtcNow;
        var version = new DatasetVersion
        {
            ImportDatasetId = importDatasetId,
            Scope = scope,
            Status = status,
            ImportJobId = importJobId
        };

        version.SetCreatedAt(now);
        version.SetUpdatedAt(now);

        return version;
    }
}
