using Bravo.Application.Acquisition.Providers;

namespace Bravo.Application.Acquisition.Services;

public interface IGeographyPersistenceService
{
    Task<PersistenceResult> UpsertStates(IReadOnlyList<IbgeStateRecord> batch, CancellationToken ct = default);

    Task<PersistenceResult> UpsertMunicipalities(IReadOnlyList<IbgeMunicipalityRecord> batch, CancellationToken ct = default);

    Task<PersistenceResult> UpsertMunicipalityTseCodes(
        IReadOnlyList<MunicipalityCrosswalkRecord> batch,
        CancellationToken ct = default);
    Task<GeographyReadiness> GetReadiness(CancellationToken ct = default);
}

public record GeographyReadiness(int States, int Municipalities)
{
    public bool IsReady => States > 0 && Municipalities > 0;
}

public record MunicipalityCrosswalkRecord(
    string StateAbbreviation,
    string TseCode,
    int IbgeCode,
    string? TseName,
    string? MesoRegionName,
    string? MicroRegionName);
