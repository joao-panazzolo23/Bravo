using Bravo.Application.Acquisition.Providers;

namespace Bravo.Application.Acquisition.Services;

public interface IGeographyPersistenceService
{
    /// <summary>Upserts a batch of states. Returns count of records inserted/updated.</summary>
    Task<int> UpsertStates(IReadOnlyList<IbgeStateRecord> batch, CancellationToken ct = default);

    /// <summary>Upserts a batch of municipalities. Returns count of records inserted/updated.</summary>
    Task<int> UpsertMunicipalities(IReadOnlyList<IbgeMunicipalityRecord> batch, CancellationToken ct = default);
}
