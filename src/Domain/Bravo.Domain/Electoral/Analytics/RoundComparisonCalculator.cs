namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Turns two round-level performances into a comparison.
/// </summary>
/// <remarks>
/// The variation of the vote count is relative, the variation of the share is absolute in
/// percentage points, and they are deliberately separate fields. Municipalities are compared only
/// where the candidate appears in both rounds, which is the correct scope for a round-to-round
/// movement of one candidacy.
///
/// Rounds of different elections are not comparable, and the calculator returns
/// <see langword="null"/> instead of mixing them. The candidate composition of the two rounds may
/// differ, so the comparison describes this candidate's own movement and never attributes an
/// eliminated candidate's votes to a survivor.
/// </remarks>
public static class RoundComparisonCalculator
{
    public static ElectionRoundComparison? Compare(
        HistoricalElectionPerformance baseline,
        HistoricalElectionPerformance compared,
        IReadOnlyList<MunicipalityContestStat> baselineMunicipalities,
        IReadOnlyList<MunicipalityContestStat> comparedMunicipalities)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(compared);

        if (baseline.ElectionId != compared.ElectionId || baseline.OfficeName != compared.OfficeName)
            return null;

        var movements = MunicipalityMovements.Compare(
            baselineMunicipalities, comparedMunicipalities, stat => stat.Percentage ?? 0m);

        return new ElectionRoundComparison(
            baseline.Year,
            baseline.ElectionId,
            baseline.OfficeName,
            baseline.Round,
            compared.Round,
            baseline.TotalVotes,
            compared.TotalVotes,
            baseline.VotePercentage,
            compared.VotePercentage,
            compared.TotalVotes - baseline.TotalVotes,
            VariationCalculator.Relative(compared.TotalVotes - baseline.TotalVotes, baseline.TotalVotes),
            compared.VotePercentage - baseline.VotePercentage,
            movements.Compared,
            movements.Improved.Count,
            movements.Declined.Count,
            movements.Unchanged);
    }
}
