using Bravo.Domain.Electoral.Entities;
namespace Bravo.Domain.Electoral.Repositories;

public interface ICandidateRepository
{
    /// <summary>
    /// Upserts identities in bulk and returns the persisted id for each natural key.
    /// </summary>
    /// <remarks>
    /// The returned dictionary is keyed by <c>CandidateIdentityKey</c> so callers can fill their
    /// cache without a second read. Keying by natural key (CPF, or name and birth date when CPF
    /// is absent) is what makes concurrent imports converge on one row per person.
    /// </remarks>
    Task<IReadOnlyDictionary<string, Guid>> UpsertIdentitiesAsync(
        IReadOnlyCollection<CandidateIdentity> identities,
        CancellationToken cancellationToken = default);

    Task<int> UpsertCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        Guid stateId,
        CancellationToken cancellationToken = default);
}
