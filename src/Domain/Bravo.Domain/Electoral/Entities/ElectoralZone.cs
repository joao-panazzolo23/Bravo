using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// An electoral zone. A zone belongs to a state and may cover more than one municipality,
/// so it is not modelled as a child of a municipality.
/// </summary>
public class ElectoralZone : Entity
{
    public Guid StateId { get; private set; }
    public string Number { get; private set; } = string.Empty;

    private ElectoralZone() { }

    public static ElectoralZone Create(Guid stateId, string number)
    {
        var zone = new ElectoralZone
        {
            StateId = stateId,
            Number = number
        };
        zone.SetCreatedAt(DateTime.UtcNow);
        zone.SetUpdatedAt(DateTime.UtcNow);
        return zone;
    }
}
