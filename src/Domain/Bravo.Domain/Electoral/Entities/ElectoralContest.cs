using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A specific contest inside a round: an office disputed within a geographic scope.
/// Presidential contests have no state; state-level contests are scoped to a state.
/// </summary>
public class ElectoralContest : Entity
{
    public Guid ElectionRoundId { get; private set; }
    public ElectionRound ElectionRound { get; private set; } = null!;
    public int OfficeCode { get; private set; }
    public string OfficeName { get; private set; } = string.Empty;
    public Guid? StateId { get; private set; }

    private ElectoralContest() { }

    public static ElectoralContest Create(Guid electionRoundId, int officeCode, string officeName, Guid? stateId)
    {
        var contest = new ElectoralContest
        {
            ElectionRoundId = electionRoundId,
            OfficeCode = officeCode,
            OfficeName = officeName,
            StateId = stateId
        };
        contest.SetCreatedAt(DateTime.UtcNow);
        contest.SetUpdatedAt(DateTime.UtcNow);
        return contest;
    }
}
