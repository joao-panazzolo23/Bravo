using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class ElectoralZone : Entity
{
    public Guid StateId { get; private set; }
    public int TseZoneNumber { get; private set; }
    public Guid? MunicipalityId { get; private set; }

    private ElectoralZone() { }

    public static ElectoralZone Create(Guid stateId, int tseZoneNumber, Guid? municipalityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tseZoneNumber);

        var zone = new ElectoralZone
        {
            StateId = stateId,
            TseZoneNumber = tseZoneNumber,
            MunicipalityId = municipalityId
        };

        var now = DateTime.UtcNow;
        zone.SetCreatedAt(now);
        zone.SetUpdatedAt(now);

        return zone;
    }
}
