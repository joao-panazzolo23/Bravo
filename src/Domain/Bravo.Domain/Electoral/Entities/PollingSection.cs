using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

/// <summary>
/// A polling section, the smallest geographic unit used to store electoral results.
/// A section belongs to a state and zone, and is installed in a municipality and,
/// when the source provides it, a polling place.
/// </summary>
public class PollingSection : Entity
{
    public Guid StateId { get; private set; }
    public string ZoneNumber { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public Guid MunicipalityId { get; private set; }
    public Guid? PollingPlaceId { get; private set; }

    private PollingSection() { }

    public static PollingSection Create(
        Guid stateId,
        string zoneNumber,
        string number,
        Guid municipalityId,
        Guid? pollingPlaceId)
    {
        var section = new PollingSection
        {
            StateId = stateId,
            ZoneNumber = zoneNumber,
            Number = number,
            MunicipalityId = municipalityId,
            PollingPlaceId = pollingPlaceId
        };
        section.SetCreatedAt(DateTime.UtcNow);
        section.SetUpdatedAt(DateTime.UtcNow);
        return section;
    }

    public void Update(Guid municipalityId, Guid? pollingPlaceId)
    {
        MunicipalityId = municipalityId;
        PollingPlaceId = pollingPlaceId;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
