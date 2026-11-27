namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Municipalities where the candidate moved into or out of first place.
/// </summary>
public sealed record MunicipalityRankShift(
    IReadOnlyList<Guid> Gained,
    IReadOnlyList<Guid> Lost);
