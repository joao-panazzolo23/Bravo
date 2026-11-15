using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A candidate vote fact for a polling section. Results are never stored as properties of
/// candidates, municipalities or elections: they are historical facts tied to a contest,
/// a section, a candidate and the acquisition that produced them.
/// </summary>
public class PollingSectionResult : Entity
{
    public Guid ElectionId { get; private set; }
    public Guid ElectionRoundId { get; private set; }
    public Guid ElectoralContestId { get; private set; }
    public Guid PollingSectionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public int Votes { get; private set; }
    public Guid? ProvenanceId { get; private set; }

    private PollingSectionResult() { }

    public static PollingSectionResult Create(
        Guid electionId,
        Guid electionRoundId,
        Guid electoralContestId,
        Guid pollingSectionId,
        Guid candidateId,
        int votes,
        Guid? provenanceId)
    {
        var result = new PollingSectionResult
        {
            ElectionId = electionId,
            ElectionRoundId = electionRoundId,
            ElectoralContestId = electoralContestId,
            PollingSectionId = pollingSectionId,
            CandidateId = candidateId,
            Votes = votes,
            ProvenanceId = provenanceId
        };
        result.SetCreatedAt(DateTime.UtcNow);
        result.SetUpdatedAt(DateTime.UtcNow);
        return result;
    }
}
