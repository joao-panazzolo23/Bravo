using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class ElectoralOffice : Entity
{
    public int TseOfficeCode { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private ElectoralOffice() { }

    public static ElectoralOffice Create(int tseOfficeCode, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tseOfficeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var office = new ElectoralOffice
        {
            TseOfficeCode = tseOfficeCode,
            Name = name
        };

        var now = DateTime.UtcNow;
        office.SetCreatedAt(now);
        office.SetUpdatedAt(now);

        return office;
    }
}
