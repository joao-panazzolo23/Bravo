namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Candidate votes and winner status of one municipality inside one contest.
/// </summary>
/// <remarks>
/// Produced once per contest and reused by the year and round comparisons, so both never recompute
/// a municipal share with a different denominator. A municipality is <see cref="Won"/> only when the
/// candidate has votes and strictly more than every rival, so a tie is not a win.
/// </remarks>
public sealed record MunicipalityContestStat(
    Guid MunicipalityId,
    long ValidVotes,
    long TargetVotes,
    decimal? Percentage,
    bool Won);
