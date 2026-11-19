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

    Task<Election?> FindByCodeAsync(
        int tseElectionCode,
        CancellationToken cancellationToken = default
    );

    Task<ElectionRound?> FindRoundAsync(
        Guid electionId,
        short round,
        CancellationToken cancellationToken = default
    );
}
