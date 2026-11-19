using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Contracts;

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
