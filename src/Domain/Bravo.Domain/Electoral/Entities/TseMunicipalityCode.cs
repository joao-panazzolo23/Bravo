using Bravo.Domain.Electoral.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Electoral.Entities;

public class TseMunicipalityCode : Entity
{
    public string RawCode { get; private set; } = string.Empty;
    public Guid StateId { get; private set; }
    public Guid? MunicipalityId { get; private set; }
    public int ElectionYear { get; private set; }
    public MunicipalityMatchMethod MatchMethod { get; private set; }
    public decimal Confidence { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime? VerifiedAt { get; private set; }

    private TseMunicipalityCode() { }

    public static TseMunicipalityCode Create(
        string rawCode,
        Guid stateId,
        Guid? municipalityId,
        int electionYear,
        MunicipalityMatchMethod matchMethod,
        decimal confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(electionYear);
        ArgumentOutOfRangeException.ThrowIfNegative(confidence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(confidence, 1m);

        var code = new TseMunicipalityCode
        {
            RawCode = rawCode,
            StateId = stateId,
            MunicipalityId = municipalityId,
            ElectionYear = electionYear,
            MatchMethod = matchMethod,
            Confidence = confidence
        };

        var now = DateTime.UtcNow;
        code.SetCreatedAt(now);
        code.SetUpdatedAt(now);

        return code;
    }
}
