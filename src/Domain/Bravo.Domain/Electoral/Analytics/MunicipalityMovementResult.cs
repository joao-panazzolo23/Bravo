namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Count and identifiers of municipalities that moved in one direction.
/// </summary>
public sealed record MunicipalityMovementResult(
    int Compared,
    IReadOnlyList<Guid> Improved,
    IReadOnlyList<Guid> Declined,
    int Unchanged);
