using Bravo.Domain.Acquisition.Entities;

namespace Bravo.Application.Acquisition.Repositories;

public interface IImportCheckpointRepository
{
    Task<ImportCheckpoint?> GetLatest(Guid executionId, string datasetKey, CancellationToken ct = default);
    Task Save(ImportCheckpoint checkpoint, CancellationToken ct = default);
}
