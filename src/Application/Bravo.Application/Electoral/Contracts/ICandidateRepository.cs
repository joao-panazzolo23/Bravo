using Bravo.Domain.Electoral.Entities;

namespace Bravo.Application.Electoral.Contracts;

public interface ICandidateRepository
{
    Task<Guid> UpsertIdentityAsync(
        CandidateIdentity identity,
        CancellationToken cancellationToken = default);

    Task<int> UpsertCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        CancellationToken cancellationToken = default);
}
