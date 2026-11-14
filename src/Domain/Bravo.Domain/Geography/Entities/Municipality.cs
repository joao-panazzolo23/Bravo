using Bravo.Shared.Entities;

namespace Bravo.Domain.Geography.Entities;

public class Municipality : Entity
{
    public int IbgeCode { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid StateId { get; private set; }
    public State State { get; private set; } = null!;

    private Municipality() { }

    public static Municipality Create(int ibgeCode, string name, Guid stateId)
    {
        var municipality = new Municipality
        {
            IbgeCode = ibgeCode,
            Name = name,
            StateId = stateId
        };
        municipality.SetCreatedAt(DateTime.UtcNow);
        municipality.SetUpdatedAt(DateTime.UtcNow);
        return municipality;
    }
}
