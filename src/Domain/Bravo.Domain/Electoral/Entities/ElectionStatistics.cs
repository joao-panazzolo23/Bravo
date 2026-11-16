using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A stored source fact: per-section turnout figures from TSE. Kept separate from
/// candidate votes so electorate totals are never duplicated per candidate.
/// </summary>
public class ElectionStatistics : Entity
{
    public Guid ElectionRoundId { get; private set; }
    public Guid SectionId { get; private set; }
    public long? EligibleVoters { get; private set; }
    public long? Attendees { get; private set; }
    public long? Absentees { get; private set; }
    public long? BlankVotes { get; private set; }
    public long? NullVotes { get; private set; }
    public Guid DatasetVersionId { get; private set; }

    private ElectionStatistics() { }

    public static ElectionStatistics Create(
        Guid electionRoundId,
        Guid sectionId,
        long? eligibleVoters,
        long? attendees,
        long? absentees,
        long? blankVotes,
        long? nullVotes,
        Guid datasetVersionId)
    {
        var statistics = new ElectionStatistics
        {
            ElectionRoundId = electionRoundId,
            SectionId = sectionId,
            EligibleVoters = eligibleVoters,
            Attendees = attendees,
            Absentees = absentees,
            BlankVotes = blankVotes,
            NullVotes = nullVotes,
            DatasetVersionId = datasetVersionId
        };

        var now = DateTime.UtcNow;
        statistics.SetCreatedAt(now);
        statistics.SetUpdatedAt(now);

        return statistics;
    }
}
