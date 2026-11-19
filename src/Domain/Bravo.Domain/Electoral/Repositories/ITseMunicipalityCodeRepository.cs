using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface ITseMunicipalityCodeRepository
{
    Task<int> UpsertAsync(
        IReadOnlyCollection<TseMunicipalityCode> codes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Guid?>> GetResolvedAsync(
        int electionYear,
        CancellationToken cancellationToken = default
    );
}
