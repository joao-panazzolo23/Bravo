using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class Federation : Entity
{
    public int TseFederationNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Acronym { get; private set; } = string.Empty;
    public string? Composition { get; private set; }

    private Federation() { }

    public static Federation Create(
        int tseFederationNumber,
        string name,
        string acronym,
        string? composition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(acronym);

        var federation = new Federation
        {
            TseFederationNumber = tseFederationNumber,
            Name = name,
            Acronym = acronym,
            Composition = composition
        };

        var now = DateTime.UtcNow;
        federation.SetCreatedAt(now);
        federation.SetUpdatedAt(now);

        return federation;
    }
}
