using Bravo.Domain.Acquisition.Entities;

namespace Bravo.Application.Acquisition.Repositories;

public interface IDatasetProvenanceRepository
{
    Task Add(DatasetProvenance provenance, CancellationToken ct = default);
    Task Update(DatasetProvenance provenance, CancellationToken ct = default);
    Task<DatasetProvenance?> GetLatest(string provider, string datasetIdentifier, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent successful acquisition of a dataset, used for conditional
    /// requests and change detection.
    /// </summary>
    Task<DatasetProvenance?> GetLatestSuccessful(string provider, string datasetIdentifier, CancellationToken ct = default);
}
