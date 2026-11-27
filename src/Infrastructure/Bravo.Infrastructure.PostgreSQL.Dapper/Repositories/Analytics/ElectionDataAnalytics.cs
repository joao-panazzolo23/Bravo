using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Dapper implementation of <see cref="IElectionDataAnalytics"/>.
/// </summary>
/// <remarks>
/// The heavy lifting is done by one query object per analysis, each of which aggregates inside
/// PostgreSQL and returns only the rows the caller asked for. This type validates the request,
/// delegates to those queries and, for the two comparisons, combines round-level aggregates with
/// municipality result sets instead of issuing one query per municipality.
///
/// Nothing here infers turnout: eligible voters are not stored, so participation fields stay
/// <see langword="null"/> rather than being estimated from votes cast.
/// </remarks>
internal sealed class ElectionDataAnalytics(
    VoteConcentrationQueries concentration,
    MunicipalityPerformanceQueries municipalityPerformance,
    MunicipalityMarginQueries municipalityMargins,
    TurnoutCompositionQueries turnout,
    SectionOutlierQueries outliers,
    RegionalDominanceQueries dominance,
    ElectionPerformanceQueries performance,
    NpgsqlDataSource dataSource) : IElectionDataAnalytics
{
    public Task<PaginatedList<PollingStationVoteConcentration>> FindVoteConcentrationAsync(
        ElectionAnalyticsFilter filter,
        decimal minimumPercentage,
        decimal? maximumPercentage,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest().RequireCandidate();
        AnalyticsScopeSql.ValidatePaging(page, pageSize);
        ValidateThresholds(minimumPercentage, maximumPercentage);

        return concentration.FindByThresholdAsync(
            filter, minimumPercentage, maximumPercentage, page, pageSize, cancellationToken);
    }

    public Task<IReadOnlyList<PollingStationVoteConcentration>> RankVoteConcentrationAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest().RequireCandidate();
        AnalyticsScopeSql.ValidateLimit(limit);

        return concentration.RankAsync(filter, limit, cancellationToken);
    }

    public Task<IReadOnlyList<MunicipalityCandidatePerformance>> GetMunicipalityPerformanceAsync(
        ElectionAnalyticsFilter filter,
        MunicipalityPerformanceSort sort,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest().RequireCandidate();
        AnalyticsScopeSql.ValidateLimit(limit);

        return municipalityPerformance.GetAsync(filter, sort, limit, cancellationToken);
    }

    public async Task<HistoricalPerformanceComparison?> CompareElectionYearsAsync(
        ElectionAnalyticsFilter filter,
        int baselineYear,
        int comparedYear,
        CancellationToken cancellationToken = default)
    {
        filter.RequireOffice();

        if (filter.ElectionId is not null)
        {
            throw new ArgumentException(
                "A year comparison must not be narrowed to a single election identifier.",
                nameof(filter));
        }

        if (baselineYear == comparedYear)
        {
            throw new ArgumentException(
                $"Baseline and compared year must differ, both were {baselineYear}.",
                nameof(comparedYear));
        }

        if (filter.CandidateId is null && filter.CandidateIdentityId is null)
        {
            throw new ArgumentException(
                "A candidate or candidate identity is required.", nameof(filter));
        }

        var baselineFilter = filter with { Year = baselineYear };
        var comparedFilter = filter with { Year = comparedYear };

        var baseline = await performance.GetPerformanceAsync(baselineFilter, cancellationToken);
        var compared = await performance.GetPerformanceAsync(comparedFilter, cancellationToken);

        if (baseline is null || compared is null || baseline.OfficeName != compared.OfficeName)
            return null;

        var baselineMunicipalities =
            await performance.GetMunicipalityStatsAsync(baselineFilter, cancellationToken);
        var comparedMunicipalities =
            await performance.GetMunicipalityStatsAsync(comparedFilter, cancellationToken);

        return YearComparisonCalculator.Compare(
            baseline, compared, baselineMunicipalities, comparedMunicipalities);
    }

    public Task<IReadOnlyList<MunicipalityElectoralMargin>> GetMunicipalityMarginsAsync(
        ElectionAnalyticsFilter filter,
        MarginDirection direction,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest();
        AnalyticsScopeSql.ValidateLimit(limit);

        return municipalityMargins.GetAsync(filter, direction, limit, cancellationToken);
    }

    public Task<IReadOnlyList<TurnoutComposition>> GetTurnoutCompositionAsync(
        ElectionAnalyticsFilter filter,
        VoteCompositionLevel level,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest();
        AnalyticsScopeSql.ValidateLimit(limit);

        return turnout.GetAsync(filter, level, limit, cancellationToken);
    }

    public Task<IReadOnlyList<SectionVoteOutlier>> DetectOutliersAsync(
        ElectionAnalyticsFilter filter,
        OutlierReferenceLevel referenceLevel,
        decimal sensitivity,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest().RequireCandidate();
        AnalyticsScopeSql.ValidateLimit(limit);

        if (sensitivity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sensitivity), sensitivity, "Sensitivity must be greater than zero.");
        }

        return outliers.GetAsync(filter, referenceLevel, sensitivity, limit, cancellationToken);
    }

    public Task<IReadOnlyList<StateElectoralDominance>> GetRegionalDominanceAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        filter.RequireContest();
        AnalyticsScopeSql.ValidateLimit(limit);

        return dominance.GetAsync(filter, limit, cancellationToken);
    }

    public async Task<ElectionRoundComparison?> CompareElectionRoundsAsync(
        ElectionAnalyticsFilter filter,
        short baselineRound,
        short comparedRound,
        CancellationToken cancellationToken = default)
    {
        filter.RequireElection().RequireOffice().RequireCandidate();

        if (baselineRound == comparedRound)
        {
            throw new ArgumentException(
                $"Baseline and compared round must differ, both were {baselineRound}.",
                nameof(comparedRound));
        }

        var baselineFilter = filter with { Round = baselineRound };
        var comparedFilter = filter with { Round = comparedRound };

        var baseline = await performance.GetPerformanceAsync(baselineFilter, cancellationToken);
        var compared = await performance.GetPerformanceAsync(comparedFilter, cancellationToken);

        if (baseline is null || compared is null)
            return null;

        var baselineMunicipalities =
            await performance.GetMunicipalityStatsAsync(baselineFilter, cancellationToken);
        var comparedMunicipalities =
            await performance.GetMunicipalityStatsAsync(comparedFilter, cancellationToken);

        return RoundComparisonCalculator.Compare(
            baseline, compared, baselineMunicipalities, comparedMunicipalities);
    }

    public async Task<CandidateIdentityHistory?> GetCandidateIdentityHistoryAsync(
        Guid candidateIdentityId,
        int? year,
        Guid? officeId,
        CancellationToken cancellationToken = default)
    {
        if (candidateIdentityId == Guid.Empty)
            throw new ArgumentException("A candidate identity is required.", nameof(candidateIdentityId));

        var contests = await EnsureIdentityExistsAsync(candidateIdentityId, cancellationToken);

        if (contests is null)
            return null;

        var candidacies = new List<HistoricalElectionPerformance>();

        foreach (var contest in contests)
        {
            if (year is not null && contest.Year != year)
                continue;

            if (officeId is not null && contest.OfficeId != officeId)
                continue;

            var filter = new ElectionAnalyticsFilter(
                Year: contest.Year,
                ElectionId: contest.ElectionId,
                OfficeId: contest.OfficeId,
                Round: contest.Round,
                CandidateIdentityId: candidateIdentityId);

            var result = await performance.GetPerformanceAsync(filter, cancellationToken);

            if (result is not null)
                candidacies.Add(result);
        }

        var fullName = await ReadIdentityNameAsync(candidateIdentityId, cancellationToken);

        if (fullName is null)
            return null;

        return IdentityHistoryBuilder.Build(candidateIdentityId, fullName, candidacies);
    }

    private static void ValidateThresholds(decimal minimumPercentage, decimal? maximumPercentage)
    {
        if (minimumPercentage is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPercentage), minimumPercentage, "The percentage must be between 0 and 100.");
        }

        if (maximumPercentage is not null
            && (maximumPercentage < minimumPercentage || maximumPercentage > 100m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPercentage), maximumPercentage,
                "The maximum percentage must be at least the minimum and at most 100.");
        }
    }

    private async Task<IReadOnlyList<IdentityContest>?> EnsureIdentityExistsAsync(
        Guid candidateIdentityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT election."Year", candidate."ElectionId", candidate."OfficeId",
                   round_row."Round"
            FROM "Candidates" AS candidate
            INNER JOIN "Elections" AS election ON election."Id" = candidate."ElectionId"
            INNER JOIN "ElectionRounds" AS round_row ON round_row."ElectionId" = election."Id"
            WHERE candidate."CandidateIdentityId" = @candidateIdentityId
            ORDER BY election."Year", round_row."Round", candidate."OfficeId";
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<IdentityContest>(
            new CommandDefinition(sql, new { candidateIdentityId }, cancellationToken: cancellationToken));

        var contests = rows.AsList();

        return contests.Count == 0 ? null : contests;
    }

    private async Task<string?> ReadIdentityNameAsync(
        Guid candidateIdentityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "FullName" FROM "CandidateIdentities" WHERE "Id" = @candidateIdentityId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, new { candidateIdentityId }, cancellationToken: cancellationToken));
    }
}
