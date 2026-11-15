using Bravo.Shared.Entities;

namespace Bravo.Domain.Geography.Entities;

/// <summary>
/// Official correspondence between a TSE municipality code and an IBGE municipality.
/// TSE codes have five digits and must be stored as text to preserve leading zeros.
/// </summary>
public class MunicipalityTseCode : Entity
{
    public Guid StateId { get; private set; }
    public string TseCode { get; private set; } = string.Empty;
    public int IbgeCode { get; private set; }
    public Guid MunicipalityId { get; private set; }
    public string? TseName { get; private set; }
    public string? MesoRegionName { get; private set; }
    public string? MicroRegionName { get; private set; }

    private MunicipalityTseCode() { }

    public static MunicipalityTseCode Create(
        Guid stateId,
        string tseCode,
        int ibgeCode,
        Guid municipalityId,
        string? tseName,
        string? mesoRegionName = null,
        string? microRegionName = null)
    {
        var mapping = new MunicipalityTseCode
        {
            StateId = stateId,
            TseCode = tseCode,
            IbgeCode = ibgeCode,
            MunicipalityId = municipalityId,
            TseName = tseName,
            MesoRegionName = mesoRegionName,
            MicroRegionName = microRegionName
        };
        mapping.SetCreatedAt(DateTime.UtcNow);
        mapping.SetUpdatedAt(DateTime.UtcNow);
        return mapping;
    }
}
