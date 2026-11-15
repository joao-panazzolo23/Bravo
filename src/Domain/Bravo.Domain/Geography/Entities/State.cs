using Bravo.Shared.Entities;

namespace Bravo.Domain.Geography.Entities;

public class State : Entity
{
    public int IbgeCode { get; private set; }
    public string Abbreviation { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;

    private State() { }

    private State(
        int ibgeCode,
        string abbreviation,
        string name,
        string region
    )
    {
        IbgeCode = ibgeCode;
        Abbreviation = abbreviation;
        Name = name;
        Region = region;
    }

    public static State Create(int ibgeCode, string abbreviation, string name, string region)
    {
        var state = new State(ibgeCode, abbreviation, name, region);
        state.SetCreatedAt(DateTime.UtcNow);
        state.SetUpdatedAt(DateTime.UtcNow);
        return state;
    }
}
