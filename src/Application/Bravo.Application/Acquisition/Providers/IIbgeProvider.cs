using Bravo.Application.Acquisition.Services;

namespace Bravo.Application.Acquisition.Providers;

/// <summary>
/// Provider contract for IBGE reference datasets (states and municipalities).
/// </summary>
public interface IIbgeProvider
{
    Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default);

    /// <summary>
    /// Downloads the states dataset to temporary storage and returns the local file.
    /// </summary>
    Task<CachedDownload> DownloadStates(Guid executionId, ConditionalRequest? conditional = null, CancellationToken ct = default);

    /// <summary>
    /// Downloads the municipalities dataset to temporary storage and returns the local file.
    /// </summary>
    Task<CachedDownload> DownloadMunicipalities(Guid executionId, ConditionalRequest? conditional = null, CancellationToken ct = default);

    IAsyncEnumerable<ParsedRecord<IbgeStateRecord>> StreamStates(string filePath, CancellationToken ct = default);

    IAsyncEnumerable<ParsedRecord<IbgeMunicipalityRecord>> StreamMunicipalities(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Returns the dataset info of the source endpoint backing a downloaded file.
    /// </summary>
    Uri GetSourceUri(DatasetScope scope);
}

public enum DatasetScope
{
    States,
    Municipalities
}
