using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Services;

namespace Bravo.Domain.Electoral.Models;

/// <summary>
/// Outcome of resolving a batch of candidate identities to their persisted rows.
/// </summary>
/// <remarks>
/// <paramref name="IdsByKey"/> is keyed by <see cref="CandidateIdentityKey"/> and contains an
/// entry for every distinct natural key in the input batch, whether the row already existed or
/// was just inserted. <paramref name="Conflicts"/> carries diagnostics for identities that
/// already existed with different immutable attributes; the canonical row is always preserved.
///
/// The diagnostics deliberately carry no CPF value: a conflict is identified by the persisted
/// identifier and by which attributes diverged, so it can be logged without exposing sensitive
/// document numbers.
/// </remarks>
public sealed record CandidateIdentityResolution(
    IReadOnlyDictionary<string, Guid> IdsByKey,
    IReadOnlyList<CandidateIdentityConflict> Conflicts)
{
    public static CandidateIdentityResolution Empty { get; } = new(
        new Dictionary<string, Guid>(StringComparer.Ordinal),
        []);

    /// <summary>
    /// The identity key of <paramref name="identity"/> exactly as the repository keys the result.
    /// </summary>
    public static string KeyOf(CandidateIdentity identity)
        => CandidateIdentityKey.Of(identity.Cpf, identity.FullName, identity.BirthDate);
}

/// <summary>
/// A stored identity whose canonical attributes differ from an incoming record that matched its
/// natural key. The row was not modified; the flags only describe the divergence.
/// </summary>
public sealed record CandidateIdentityConflict(
    Guid CandidateIdentityId,
    bool FullNameDiffers,
    bool BirthDateDiffers
);
