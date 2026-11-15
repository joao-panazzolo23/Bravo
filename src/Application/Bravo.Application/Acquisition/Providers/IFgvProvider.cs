using Bravo.Application.Acquisition.Services;

namespace Bravo.Application.Acquisition.Providers;

/// <summary>
/// Provider contract for FGV complementary reference datasets.
/// The only dataset used by the pipeline is the official IBGE/TSE municipality
/// crosswalk published as an auxiliary table of the CEPESP/FGV electoral data.
/// </summary>
public interface IFgvProvider
{
    Task<FgvDatasetInfo> GetDatasetInfo(CancellationToken ct = default);

    Task<CachedDownload> Download(Guid executionId, FgvDatasetInfo dataset, CancellationToken ct = default);

    IAsyncEnumerable<ParsedRecord<FgvMunicipalityRecord>> StreamMunicipalities(string filePath, CancellationToken ct = default);
}

public record FgvDatasetInfo(
    string DatasetIdentifier,
    string DisplayName,
    Uri SourceUri,
    string Format,
    string? Version = null
);

public record FgvMunicipalityRecord(
    string StateAbbreviation,
    string TseCode,
    int IbgeCode,
    string? MunicipalityName,
    int? UfIbgeCode,
    string? MesoRegionName,
    string? MicroRegionName);
