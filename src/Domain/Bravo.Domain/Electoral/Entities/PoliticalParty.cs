using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class PoliticalParty : Entity
{
    public int TsePartyNumber { get; private set; }
    public string Acronym { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    private PoliticalParty() { }

    public static PoliticalParty Create(int tsePartyNumber, string acronym, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acronym);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var party = new PoliticalParty
        {
            TsePartyNumber = tsePartyNumber,
            Acronym = acronym,
            Name = name
        };

        var now = DateTime.UtcNow;
        party.SetCreatedAt(now);
        party.SetUpdatedAt(now);

        return party;
    }
}
