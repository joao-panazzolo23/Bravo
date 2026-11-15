using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A polling place. The TSE local code is only unique inside a municipality and zone, so the
/// natural key includes both. The name is nullable because older datasets publish only the
/// local code.
/// </summary>
public class PollingPlace : Entity
{
    public Guid MunicipalityId { get; private set; }
    public string ZoneNumber { get; private set; } = string.Empty;
    public string TseCode { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public string? Address { get; private set; }

    private PollingPlace() { }

    public static PollingPlace Create(
        Guid municipalityId,
        string zoneNumber,
        string tseCode,
        string? name,
        string? address)
    {
        var place = new PollingPlace
        {
            MunicipalityId = municipalityId,
            ZoneNumber = zoneNumber,
            TseCode = tseCode,
            Name = name,
            Address = address
        };
        place.SetCreatedAt(DateTime.UtcNow);
        place.SetUpdatedAt(DateTime.UtcNow);
        return place;
    }

    public void Update(string? name, string? address)
    {
        Name = name;
        Address = address;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
