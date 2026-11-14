using Bravo.Shared.Entities;

namespace Bravo.Domain.Acquisition.Entities;

public class ImportCheckpoint : Entity
{
    public Guid ExecutionId { get; private set; }
    public ImportExecution Execution { get; private set; } = null!;
    public string DatasetKey { get; private set; } = string.Empty;
    public string CheckpointData { get; private set; } = string.Empty;
    public int RecordsCompleted { get; private set; }
    public DateTime CheckpointedAt { get; private set; }

    private ImportCheckpoint() { }

    public static ImportCheckpoint Create(Guid executionId, string datasetKey, string checkpointData, int recordsCompleted)
    {
        var checkpoint = new ImportCheckpoint
        {
            ExecutionId = executionId,
            DatasetKey = datasetKey,
            CheckpointData = checkpointData,
            RecordsCompleted = recordsCompleted,
            CheckpointedAt = DateTime.UtcNow
        };
        checkpoint.SetCreatedAt(DateTime.UtcNow);
        checkpoint.SetUpdatedAt(DateTime.UtcNow);
        return checkpoint;
    }
}
