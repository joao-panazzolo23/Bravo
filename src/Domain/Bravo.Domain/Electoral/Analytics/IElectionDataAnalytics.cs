using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;

namespace Bravo.Domain.Electoral.Analytics;

/// <summary>
/// Read-only analytical access to stored election results.
/// </summary>
/// <remarks>
/// Every operation is scoped to one office and one round through <see cref="ElectionAnalyticsFilter"/>
/// so different contests and turns are never aggregated together. A share divides by valid votes
/// (nominal plus party ballots); blank and null ballots are reported separately and never enter a
/// candidate denominator. Only results of the active dataset versions are read, which prevents a
/// re-import from being counted twice.
///
/// Implementations are expected to aggregate inside the database; the contract returns only the
/// rows an analysis needs and never exposes persistence types.
/// </remarks>
public interface IElectionDataAnalytics
{
    /// <summary>
    /// Sections where the filtered candidate or identity reached at least
    /// <paramref name="minimumPercentage"/> of valid votes, optionally capped by
    /// <paramref name="maximumPercentage"/>.
    /// </summary>
    /// <example>
    /// <c>FindVoteConcentrationAsync(filter with OfficeId and Round=2, 90m, page: 1, pageSize: 50)</c>
    /// </example>
    Task<PaginatedList<PollingStationVoteConcentration>> FindVoteConcentrationAsync(
        ElectionAnalyticsFilter filter,
        decimal minimumPercentage,
        decimal? maximumPercentage,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sections ordered by the filtered candidate's share of valid votes, highest first.
    /// </summary>
    Task<IReadOnlyList<PollingStationVoteConcentration>> RankVoteConcentrationAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The filtered candidate's performance per municipality, ranked by share or vote count.
    /// </summary>
    Task<IReadOnlyList<MunicipalityCandidatePerformance>> GetMunicipalityPerformanceAsync(
        ElectionAnalyticsFilter filter,
        MunicipalityPerformanceSort sort,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares the filtered candidate or identity between two election years of the same office.
    /// </summary>
    Task<HistoricalPerformanceComparison?> CompareElectionYearsAsync(
        ElectionAnalyticsFilter filter,
        int baselineYear,
        int comparedYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Municipalities ordered by margin between their first and second candidates.
    /// </summary>
    Task<IReadOnlyList<MunicipalityElectoralMargin>> GetMunicipalityMarginsAsync(
        ElectionAnalyticsFilter filter,
        MarginDirection direction,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vote composition aggregated at the requested geographic level.
    /// </summary>
    Task<IReadOnlyList<TurnoutComposition>> GetTurnoutCompositionAsync(
        ElectionAnalyticsFilter filter,
        VoteCompositionLevel level,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sections whose candidate share deviates from their reference population by at least
    /// <paramref name="sensitivity"/> standard deviations.
    /// </summary>
    Task<IReadOnlyList<SectionVoteOutlier>> DetectOutliersAsync(
        ElectionAnalyticsFilter filter,
        OutlierReferenceLevel referenceLevel,
        decimal sensitivity,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dominance of the filtered candidate per state, computed from municipality results.
    /// </summary>
    Task<IReadOnlyList<StateElectoralDominance>> GetRegionalDominanceAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two rounds of one election for the filtered candidate.
    /// </summary>
    Task<ElectionRoundComparison?> CompareElectionRoundsAsync(
        ElectionAnalyticsFilter filter,
        short baselineRound,
        short comparedRound,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every candidacy of one candidate identity across elections.
    /// </summary>
    Task<CandidateIdentityHistory?> GetCandidateIdentityHistoryAsync(
        Guid candidateIdentityId,
        int? year,
        Guid? officeId,
        CancellationToken cancellationToken = default);
}
