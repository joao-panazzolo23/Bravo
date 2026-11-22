using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
namespace Bravo.Domain.Electoral.Repositories;

public interface ICandidateRepository
{
    /// <summary>
    /// Inserts the identities that do not exist yet and returns the persisted id for every
    /// natural key in the batch, without ever updating an existing row.
    /// </summary>
    /// <remarks>
    /// <see cref="CandidateIdentity"/> is immutable: the first record that establishes a natural
    /// key becomes the canonical one, and a later import resolves to it instead of overwriting
    /// its attributes. The result is keyed by <see cref="Services.CandidateIdentityKey"/> so
    /// callers can fill their cache without a second read. Conflicts between incoming attributes
    /// and the stored row are reported, never applied.
    /// </remarks>
    Task<CandidateIdentityResolution> EnsureIdentitiesAsync(
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
