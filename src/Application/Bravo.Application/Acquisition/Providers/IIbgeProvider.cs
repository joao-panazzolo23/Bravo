namespace Bravo.Application.Acquisition.Providers;

public record IbgeStateRecord(int Code, string Abbreviation, string Name, string Region);

public record IbgeMunicipalityRecord(int Code, string Name, int StateCode);

public record IbgeDatasetInfo(
    string StatesUrl,
    string MunicipalitiesUrl,
    string? Version,
    string Format
);

public interface IIbgeProvider
{
    /// <summary>Returns metadata about the dataset (URL, expected format, version hint).</summary>
    Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default);

    /// <summary>Streams parsed state records incrementally.</summary>
    IAsyncEnumerable<IbgeStateRecord> StreamStates(CancellationToken ct = default);

    /// <summary>Streams parsed municipality records incrementally.</summary>
    IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalities(CancellationToken ct = default);
}
