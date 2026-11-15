using Bravo.Domain.Acquisition.Entities;

namespace Bravo.Application.Acquisition.Repositories;

public interface IImportCheckpointRepository
{
    Task<ImportCheckpoint?> GetLatest(Guid executionId, string datasetKey, CancellationToken ct = default);

    /// <summary>
    /// Returns the dataset keys already completed for an execution. Used to skip work that
    /// was persisted and checkpointed by a previous attempt.
    /// </summary>
    Task<IReadOnlySet<string>> GetCompletedKeys(Guid executionId, CancellationToken ct = default);

    Task Save(ImportCheckpoint checkpoint, CancellationToken ct = default);
}
