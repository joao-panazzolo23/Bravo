using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface IElectionRepository
{
    Task<Election> UpsertAsync(Election election, CancellationToken cancellationToken = default);

    Task<ElectionRound> UpsertRoundAsync(
        ElectionRound round,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists every election. The set is small and bounded by the TSE calendar, so import stages
    /// load it once instead of looking elections up per record.
    /// </summary>
    Task<IReadOnlyList<Election>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every election round, matching <see cref="ListAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ElectionRound>> ListRoundsAsync(
        CancellationToken cancellationToken = default
    );
}
