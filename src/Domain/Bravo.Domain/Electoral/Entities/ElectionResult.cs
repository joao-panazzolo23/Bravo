using Bravo.Domain.Electoral.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A stored source fact: votes reported by TSE for a round, section and office.
/// Never populated with computed totals.
/// </summary>
public class ElectionResult : Entity
{
    public Guid ElectionRoundId { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid OfficeId { get; private set; }
    public VoteType VoteType { get; private set; }
    public Guid? CandidateId { get; private set; }
    public Guid? PartyId { get; private set; }
    public int TseBallotNumber { get; private set; }
    public long Votes { get; private set; }
    public Guid DatasetVersionId { get; private set; }

    private ElectionResult() { }

    public static ElectionResult Create(
        Guid electionRoundId,
        Guid sectionId,
        Guid officeId,
        VoteType voteType,
        Guid? candidateId,
        Guid? partyId,
        int tseBallotNumber,
        long votes,
        Guid datasetVersionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(votes);
        ValidateReferences(voteType, candidateId, partyId);

        var result = new ElectionResult
        {
            ElectionRoundId = electionRoundId,
            SectionId = sectionId,
            OfficeId = officeId,
            VoteType = voteType,
            CandidateId = candidateId,
            PartyId = partyId,
            TseBallotNumber = tseBallotNumber,
            Votes = votes,
            DatasetVersionId = datasetVersionId
        };

        var now = DateTime.UtcNow;
        result.SetCreatedAt(now);
        result.SetUpdatedAt(now);

        return result;
    }

    private static void ValidateReferences(VoteType voteType, Guid? candidateId, Guid? partyId)
    {
        switch (voteType)
        {
            case VoteType.Nominal when candidateId is null:
                throw new ArgumentException("A nominal vote must reference a candidate.", nameof(candidateId));

            case VoteType.Party when partyId is null:
                throw new ArgumentException("A party vote must reference a party.", nameof(partyId));

            case VoteType.Blank or VoteType.Null when candidateId is not null || partyId is not null:
                throw new ArgumentException(
                    "Blank and null votes must not reference a candidate or party.");
        }
    }
}
