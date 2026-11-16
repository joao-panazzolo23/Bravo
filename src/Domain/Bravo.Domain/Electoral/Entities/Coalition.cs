using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class Coalition : Entity
{
    public Guid ElectionRoundId { get; private set; }
    public Guid OfficeId { get; private set; }
    public Guid StateId { get; private set; }
    public long TseCoalitionNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Composition { get; private set; }

    private Coalition() { }

    public static Coalition Create(
        Guid electionRoundId,
        Guid officeId,
        Guid stateId,
        long tseCoalitionNumber,
        string name,
        string? composition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var coalition = new Coalition
        {
            ElectionRoundId = electionRoundId,
            OfficeId = officeId,
            StateId = stateId,
            TseCoalitionNumber = tseCoalitionNumber,
            Name = name,
            Composition = composition
        };

        var now = DateTime.UtcNow;
        coalition.SetCreatedAt(now);
        coalition.SetUpdatedAt(now);

        return coalition;
    }
}
