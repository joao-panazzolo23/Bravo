namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Compares the same candidate across two municipality result sets.
/// </summary>
/// <remarks>
/// Only municipalities present in both sets are inspected. A municipality the candidate did not
/// contest in one of the rounds says nothing about movement, so counting it as a gain or a decline
/// would invent a trend. Ties are counted separately from improvements.
///
/// Rank shifts are restricted to first place, which is the only position that makes "won" and
/// "lost" well defined across different candidate compositions.
/// </remarks>
public static class MunicipalityMovements
{
    public static MunicipalityMovementResult Compare(
        IReadOnlyList<MunicipalityContestStat> baseline,
        IReadOnlyList<MunicipalityContestStat> compared,
        Func<MunicipalityContestStat, decimal> percentageOf)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(compared);
        ArgumentNullException.ThrowIfNull(percentageOf);

        var comparedById = compared.ToDictionary(stat => stat.MunicipalityId);
        var improved = new List<Guid>();
        var declined = new List<Guid>();
        var unchanged = 0;

        foreach (var baselineStat in baseline)
        {
            if (!comparedById.TryGetValue(baselineStat.MunicipalityId, out var comparedStat))
                continue;

            var difference = percentageOf(comparedStat) - percentageOf(baselineStat);

            if (difference > 0)
                improved.Add(baselineStat.MunicipalityId);
            else if (difference < 0)
                declined.Add(baselineStat.MunicipalityId);
            else
                unchanged++;
        }

        return new MunicipalityMovementResult(
            improved.Count + declined.Count + unchanged,
            improved,
            declined,
            unchanged);
    }

    public static MunicipalityRankShift RankShifts(
        IReadOnlyList<MunicipalityContestStat> baseline,
        IReadOnlyList<MunicipalityContestStat> compared)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(compared);

        var comparedById = compared.ToDictionary(stat => stat.MunicipalityId);
        var gained = new List<Guid>();
        var lost = new List<Guid>();

        foreach (var baselineStat in baseline)
        {
            if (!comparedById.TryGetValue(baselineStat.MunicipalityId, out var comparedStat))
                continue;

            if (comparedStat.Won && !baselineStat.Won)
                gained.Add(baselineStat.MunicipalityId);
            else if (baselineStat.Won && !comparedStat.Won)
                lost.Add(baselineStat.MunicipalityId);
        }

        return new MunicipalityRankShift(gained, lost);
    }
}
