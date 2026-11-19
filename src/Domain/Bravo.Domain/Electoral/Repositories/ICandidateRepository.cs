using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Domain.Electoral.Repositories;

public interface ICandidateRepository
{
    Task<Guid> UpsertIdentityAsync(
        CandidateIdentity identity,
        CancellationToken cancellationToken = default);

    Task<int> UpsertCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        Guid stateId,
        CancellationToken cancellationToken = default);
}
