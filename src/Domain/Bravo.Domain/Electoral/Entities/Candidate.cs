using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A candidate registered for an election. The TSE sequential code identifies the
/// candidate within an election and is stored as text to preserve the official value.
/// </summary>
public class Candidate : Entity
{
    public Guid ElectionId { get; private set; }
    public Election Election { get; private set; } = null!;
    public string TseSequential { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BallotName { get; private set; } = string.Empty;
    public int OfficeCode { get; private set; }
    public string OfficeName { get; private set; } = string.Empty;
    public Guid? PoliticalPartyId { get; private set; }
    public Guid? StateId { get; private set; }
    public int? SituationCode { get; private set; }
    public string? SituationDescription { get; private set; }
    public DateOnly? BirthDate { get; private set; }

    private Candidate() { }

    public static Candidate Create(
        Guid electionId,
        string tseSequential,
        string number,
        string name,
        string ballotName,
        int officeCode,
        string officeName,
        Guid? politicalPartyId,
        Guid? stateId,
        int? situationCode,
        string? situationDescription,
        DateOnly? birthDate)
    {
        var candidate = new Candidate
        {
            ElectionId = electionId,
            TseSequential = tseSequential,
            Number = number,
            Name = name,
            BallotName = ballotName,
            OfficeCode = officeCode,
            OfficeName = officeName,
            PoliticalPartyId = politicalPartyId,
            StateId = stateId,
            SituationCode = situationCode,
            SituationDescription = situationDescription,
            BirthDate = birthDate
        };
        candidate.SetCreatedAt(DateTime.UtcNow);
        candidate.SetUpdatedAt(DateTime.UtcNow);
        return candidate;
    }

    public void Update(
        string number,
        string name,
        string ballotName,
        int officeCode,
        string officeName,
        Guid? politicalPartyId,
        Guid? stateId,
        int? situationCode,
        string? situationDescription,
        DateOnly? birthDate)
    {
        Number = number;
        Name = name;
        BallotName = ballotName;
        OfficeCode = officeCode;
        OfficeName = officeName;
        PoliticalPartyId = politicalPartyId;
        StateId = stateId;
        SituationCode = situationCode;
        SituationDescription = situationDescription;
        BirthDate = birthDate;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
