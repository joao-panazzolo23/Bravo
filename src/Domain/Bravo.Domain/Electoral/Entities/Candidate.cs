using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// An election-specific candidate registration (candidacy). The same person can hold many
/// registrations across elections without sharing this row's identifier.
/// </summary>
public class Candidate : Entity
{
    public Guid ElectionId { get; private set; }
    public Guid OfficeId { get; private set; }
    public Guid StateId { get; private set; }
    public Guid? CandidateIdentityId { get; private set; }
    public string TseCandidateSequential { get; private set; } = string.Empty;
    public int BallotNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string UrnName { get; private set; } = string.Empty;
    public string? SocialName { get; private set; }
    public Guid? PartyId { get; private set; }
    public Guid? FederationId { get; private set; }
    public Guid? CoalitionId { get; private set; }
    public string? RegistrationStatus { get; private set; }
    public string? Gender { get; private set; }
    public string? Education { get; private set; }
    public string? Race { get; private set; }
    public string? Occupation { get; private set; }
    public DateOnly? BirthDate { get; private set; }

    private Candidate() { }

    public static Candidate Create(
        Guid electionId,
        Guid officeId,
        Guid stateId,
        Guid? candidateIdentityId,
        string tseCandidateSequential,
        int ballotNumber,
        string name,
        string urnName,
        string? socialName,
        Guid? partyId,
        Guid? federationId,
        Guid? coalitionId,
        string? registrationStatus,
        string? gender,
        string? education,
        string? race,
        string? occupation,
        DateOnly? birthDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // TSE leaves the urn name blank for some historical candidacies (verified in 2000),
        // so it falls back to the full name rather than rejecting a valid registration.
        var effectiveUrnName = string.IsNullOrWhiteSpace(urnName) ? name : urnName;

        var candidate = new Candidate
        {
            ElectionId = electionId,
            OfficeId = officeId,
            StateId = stateId,
            CandidateIdentityId = candidateIdentityId,
            TseCandidateSequential = tseCandidateSequential,
            BallotNumber = ballotNumber,
            Name = name,
            UrnName = effectiveUrnName,
            SocialName = socialName,
            PartyId = partyId,
            FederationId = federationId,
            CoalitionId = coalitionId,
            RegistrationStatus = registrationStatus,
            Gender = gender,
            Education = education,
            Race = race,
            Occupation = occupation,
            BirthDate = birthDate
        };

        var now = DateTime.UtcNow;
        candidate.SetCreatedAt(now);
        candidate.SetUpdatedAt(now);

        return candidate;
    }
}
