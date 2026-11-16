using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class ElectoralSection : Entity
{
    public Guid MunicipalityId { get; private set; }
    public Guid ZoneId { get; private set; }
    public int TseSectionNumber { get; private set; }

    private ElectoralSection() { }

    public static ElectoralSection Create(Guid municipalityId, Guid zoneId, int tseSectionNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tseSectionNumber);

        var section = new ElectoralSection
        {
            MunicipalityId = municipalityId,
            ZoneId = zoneId,
            TseSectionNumber = tseSectionNumber
        };

        var now = DateTime.UtcNow;
        section.SetCreatedAt(now);
        section.SetUpdatedAt(now);

        return section;
    }
}
