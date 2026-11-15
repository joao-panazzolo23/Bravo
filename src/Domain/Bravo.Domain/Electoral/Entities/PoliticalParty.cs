using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A political party identified by its official TSE number.
/// </summary>
public class PoliticalParty : Entity
{
    public int Number { get; private set; }
    public string Acronym { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    private PoliticalParty() { }

    public static PoliticalParty Create(int number, string acronym, string name)
    {
        var party = new PoliticalParty
        {
            Number = number,
            Acronym = acronym,
            Name = name
        };
        party.SetCreatedAt(DateTime.UtcNow);
        party.SetUpdatedAt(DateTime.UtcNow);
        return party;
    }

    public void Update(string acronym, string name)
    {
        Acronym = acronym;
        Name = name;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
