namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Compares the same candidate between two election years of one office.
/// </summary>
/// <remarks>
/// Incompatible contests are rejected instead of compared: a different office changes the valid-vote
/// denominator and the set of rivals, so a share difference between the two would be meaningless.
/// The builder therefore returns <see langword="null"/> in that case, which the caller surfaces as
/// "not comparable" rather than as a number that looks meaningful.
/// </remarks>
public static class YearComparisonCalculator
{
    public static HistoricalPerformanceComparison? Compare(
        HistoricalElectionPerformance baseline,
        HistoricalElectionPerformance compared,
        IReadOnlyList<MunicipalityContestStat> baselineMunicipalities,
        IReadOnlyList<MunicipalityContestStat> comparedMunicipalities)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(compared);

        if (baseline.OfficeName != compared.OfficeName)
            return null;

        var movements = MunicipalityMovements.Compare(
            baselineMunicipalities, comparedMunicipalities, stat => stat.Percentage ?? 0m);
        var shifts = MunicipalityMovements.RankShifts(
            baselineMunicipalities, comparedMunicipalities);

        return new HistoricalPerformanceComparison(
            compared.CandidateId ?? baseline.CandidateId,
            compared.CandidateIdentityId ?? baseline.CandidateIdentityId,
            compared.CandidateName,
            compared.OfficeName,
            baseline,
            compared,
            compared.TotalVotes - baseline.TotalVotes,
            VariationCalculator.Relative(compared.TotalVotes - baseline.TotalVotes, baseline.TotalVotes),
            compared.VotePercentage - baseline.VotePercentage,
            shifts.Gained,
            shifts.Lost,
            movements.Improved,
            movements.Declined);
    }
}
