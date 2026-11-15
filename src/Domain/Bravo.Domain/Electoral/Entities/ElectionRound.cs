using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A round of an election (first or second round).
/// </summary>
public class ElectionRound : Entity
{
    public Guid ElectionId { get; private set; }
    public Election Election { get; private set; } = null!;
    public int Number { get; private set; }

    public ICollection<ElectoralContest> Contests { get; private set; } = [];

    private ElectionRound() { }

    public static ElectionRound Create(Guid electionId, int number)
    {
        var round = new ElectionRound
        {
            ElectionId = electionId,
            Number = number
        };
        round.SetCreatedAt(DateTime.UtcNow);
        round.SetUpdatedAt(DateTime.UtcNow);
        return round;
    }
}
