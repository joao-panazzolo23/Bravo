using Bravo.Shared.Entities;

namespace Bravo.Domain.Geography.Entities;

public class State : Entity
{
    public int IbgeCode { get; private set; }
    public string Abbreviation { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;

    private State() { }

    public static State Create(int ibgeCode, string abbreviation, string name, string region)
    {
        var state = new State
        {
            IbgeCode = ibgeCode,
            Abbreviation = abbreviation,
            Name = name,
            Region = region
        };
        state.SetCreatedAt(DateTime.UtcNow);
        state.SetUpdatedAt(DateTime.UtcNow);
        return state;
    }
}
