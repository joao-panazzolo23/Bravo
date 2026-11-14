using Bravo.Application.Acquisition.Providers;

namespace Bravo.Application.Acquisition.Services;

public interface IGeographyPersistenceService
{
    Task<int> UpsertStates(IReadOnlyList<IbgeStateRecord> batch, CancellationToken ct = default);

    Task<int> UpsertMunicipalities(IReadOnlyList<IbgeMunicipalityRecord> batch, CancellationToken ct = default);
}
