namespace Bravo.Application.Acquisition.Providers;

public interface IIbgeProvider
{
    Task<IbgeDatasetInfo> GetDatasetInfo(CancellationToken ct = default);

    IAsyncEnumerable<IbgeStateRecord> StreamStates(CancellationToken ct = default);

    IAsyncEnumerable<IbgeMunicipalityRecord> StreamMunicipalities(CancellationToken ct = default);
}
