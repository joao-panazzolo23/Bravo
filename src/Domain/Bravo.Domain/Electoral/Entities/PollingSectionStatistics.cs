using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// Turnout statistics for a contest in a polling section.
/// Candidate votes are kept in <see cref="PollingSectionResult"/> so that eligible-voter,
/// attendance and blank/null totals are not repeated for every candidate.
/// Fields are nullable because not every historical dataset publishes every measure.
/// </summary>
public class PollingSectionStatistics : Entity
{
    public Guid ElectionId { get; private set; }
    public Guid ElectionRoundId { get; private set; }
    public Guid ElectoralContestId { get; private set; }
    public Guid PollingSectionId { get; private set; }
    public int? EligibleVoters { get; private set; }
    public int? Attendees { get; private set; }
    public int? Absentees { get; private set; }
    public int? NominalVotes { get; private set; }
    public int? BlankVotes { get; private set; }
    public int? NullVotes { get; private set; }
    public int? PartyVotes { get; private set; }
    public Guid? ProvenanceId { get; private set; }

    private PollingSectionStatistics() { }

    public static PollingSectionStatistics Create(
        Guid electionId,
        Guid electionRoundId,
        Guid electoralContestId,
        Guid pollingSectionId,
        int? eligibleVoters,
        int? attendees,
        int? absentees,
        int? nominalVotes,
        int? blankVotes,
        int? nullVotes,
        int? partyVotes,
        Guid? provenanceId)
    {
        var statistics = new PollingSectionStatistics
        {
            ElectionId = electionId,
            ElectionRoundId = electionRoundId,
            ElectoralContestId = electoralContestId,
            PollingSectionId = pollingSectionId,
            EligibleVoters = eligibleVoters,
            Attendees = attendees,
            Absentees = absentees,
            NominalVotes = nominalVotes,
            BlankVotes = blankVotes,
            NullVotes = nullVotes,
            PartyVotes = partyVotes,
            ProvenanceId = provenanceId
        };
        statistics.SetCreatedAt(DateTime.UtcNow);
        statistics.SetUpdatedAt(DateTime.UtcNow);
        return statistics;
    }
}
