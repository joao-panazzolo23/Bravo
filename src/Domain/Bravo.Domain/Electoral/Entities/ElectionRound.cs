using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class ElectionRound : Entity
{
    public Guid ElectionId { get; private set; }
    public short Round { get; private set; }
    public bool IsRunoff { get; private set; }

    private ElectionRound() { }

    public static ElectionRound Create(Guid electionId, short round)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);

        var electionRound = new ElectionRound
        {
            ElectionId = electionId,
            Round = round,
            IsRunoff = round > 1
        };

        var now = DateTime.UtcNow;
        electionRound.SetCreatedAt(now);
        electionRound.SetUpdatedAt(now);

        return electionRound;
    }
}
