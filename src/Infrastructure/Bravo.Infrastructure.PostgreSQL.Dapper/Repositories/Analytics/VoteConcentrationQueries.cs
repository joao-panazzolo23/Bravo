using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Section-level concentration of one candidate's nominal votes.
/// </summary>
/// <remarks>
/// The threshold query and the ranking read the same CTE chain; only the predicate and the ordering
/// differ, so the percentage a caller filters on is exactly the one the ranking sorts by.
///
/// The denominator is the valid votes of the section in the same round and office, read from the
/// active dataset version only. The opposing count is the nominal votes of every other candidate,
/// never the complement of the target's votes, because party (legend) votes are valid but belong to
/// no candidate and must not inflate a rival's total.
///
/// PostgreSQL sums <c>bigint</c> to <c>numeric</c>, so every aggregate mapped to a <c>long</c> is
/// cast back explicitly; otherwise Dapper would look for a decimal constructor.
/// </remarks>
internal sealed class VoteConcentrationQueries(NpgsqlDataSource dataSource)
{
    private static string Cte => $"""
        WITH {AnalyticsScopeSql.TargetCandidates}
        {AnalyticsScopeSql.ScopedResults}
        target AS (
            SELECT "CandidateId" FROM target_candidates ORDER BY "CandidateId" LIMIT 1
        ),
        section_valid AS (
            SELECT "SectionId", SUM("Votes")::bigint AS "ValidVotes"
            FROM scoped
            WHERE "VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
            GROUP BY "SectionId"
        ),
        section_candidate AS (
            SELECT scoped."SectionId",
                   (SUM(scoped."Votes") FILTER (WHERE EXISTS (
                       SELECT 1 FROM target_candidates AS target
                       WHERE target."CandidateId" = scoped."CandidateId")))::bigint AS "CandidateVotes",
                   (SUM(scoped."Votes") FILTER (WHERE NOT EXISTS (
                       SELECT 1 FROM target_candidates AS target
                       WHERE target."CandidateId" = scoped."CandidateId")))::bigint AS "OpposingVotes"
            FROM scoped
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
            GROUP BY scoped."SectionId"
        ),
        sections AS (
            SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round", "IsRunoff",
                   "SectionId", "TseSectionNumber", "PollingPlaceId", "TsePollingPlaceNumber",
                   "PollingPlaceName", "MunicipalityId", "MunicipalityName", "StateId",
                   "StateAbbreviation", "ZoneId", "TseZoneNumber"
            FROM scoped
        ),
        shares AS (
            SELECT sections."Year", sections."ElectionId", sections."OfficeName",
                   sections."Round", sections."IsRunoff",
                   sections."SectionId", sections."TseSectionNumber",
                   sections."PollingPlaceId", sections."TsePollingPlaceNumber",
                   sections."PollingPlaceName", sections."MunicipalityId",
                   sections."MunicipalityName", sections."StateId", sections."StateAbbreviation",
                   sections."ZoneId", sections."TseZoneNumber",
                   target."CandidateId",
                   COALESCE(valid."ValidVotes", 0) AS "ValidVotes",
                   COALESCE(candidate."CandidateVotes", 0) AS "CandidateVotes",
                   COALESCE(candidate."OpposingVotes", 0) AS "OpposingVotes"
            FROM sections
            CROSS JOIN target
            LEFT JOIN section_candidate AS candidate
                ON candidate."SectionId" = sections."SectionId"
            LEFT JOIN section_valid AS valid ON valid."SectionId" = sections."SectionId"
        )
        """;

    private const string Projection = """
        SELECT shares."Year" AS "Year",
               shares."ElectionId" AS "ElectionId",
               shares."OfficeName" AS "OfficeName",
               shares."Round" AS "Round",
               shares."IsRunoff" AS "IsRunoff",
               shares."SectionId" AS "SectionId",
               shares."TseSectionNumber" AS "TseSectionNumber",
               shares."PollingPlaceId" AS "PollingPlaceId",
               shares."TsePollingPlaceNumber" AS "TsePollingPlaceNumber",
               shares."PollingPlaceName" AS "PollingPlaceName",
               shares."MunicipalityId" AS "MunicipalityId",
               shares."MunicipalityName" AS "MunicipalityName",
               shares."StateId" AS "StateId",
               shares."StateAbbreviation" AS "StateAbbreviation",
               shares."ZoneId" AS "ZoneId",
               shares."TseZoneNumber" AS "TseZoneNumber",
               candidate_c."Id" AS "CandidateId",
               candidate_c."Name" AS "CandidateName",
               candidate_c."UrnName" AS "CandidateUrnName",
               candidate_c."CandidateIdentityId" AS "CandidateIdentityId",
               identity."FullName" AS "CandidateIdentityName",
               candidate_c."PartyId" AS "PartyId",
               party."Acronym" AS "PartyAcronym",
               shares."ValidVotes" AS "ValidVotes",
               shares."CandidateVotes" AS "CandidateVotes",
               shares."OpposingVotes" AS "OpposingVotes",
               COALESCE(ROUND((100.0 * shares."CandidateVotes" / NULLIF(shares."ValidVotes", 0))::numeric, 2), 0) AS "CandidatePercentage",
               COALESCE(ROUND((100.0 * shares."OpposingVotes" / NULLIF(shares."ValidVotes", 0))::numeric, 2), 0) AS "OpposingPercentage",
               (shares."CandidateVotes" - shares."OpposingVotes") AS "DifferenceVotes",
               COALESCE(ROUND((100.0 * (shares."CandidateVotes" - shares."OpposingVotes")
                     / NULLIF(shares."ValidVotes", 0))::numeric, 2), 0) AS "DifferencePercentagePoints"
        FROM {0} AS shares
        INNER JOIN "Candidates" AS candidate_c ON candidate_c."Id" = shares."CandidateId"
        LEFT JOIN "CandidateIdentities" AS identity ON identity."Id" = candidate_c."CandidateIdentityId"
        LEFT JOIN "PoliticalParties" AS party ON party."Id" = candidate_c."PartyId"
        """;

    private const string Ordering = """
        ORDER BY "CandidatePercentage" DESC, "CandidateVotes" DESC,
                 "StateAbbreviation", "MunicipalityName", "TseZoneNumber", "TseSectionNumber"
        """;

    public async Task<PaginatedList<PollingStationVoteConcentration>> FindByThresholdAsync(
        ElectionAnalyticsFilter filter,
        decimal minimumPercentage,
        decimal? maximumPercentage,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("MinimumPercentage", minimumPercentage);
        parameters.Add("MaximumPercentage", maximumPercentage);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var itemsSql = $"""
            {Cte},
            filtered AS (
                SELECT * FROM shares
                WHERE COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) >= @MinimumPercentage
                  AND (@MaximumPercentage::numeric IS NULL
                       OR COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) <= @MaximumPercentage::numeric)
                ORDER BY COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) DESC,
                         "StateAbbreviation", "MunicipalityName", "TseZoneNumber", "TseSectionNumber"
                LIMIT @PageSize OFFSET @Offset
            )
            {string.Format(Projection, "filtered")}
            {Ordering};
            """;

        var countSql = $"""
            {Cte}
            SELECT COUNT(*)::int FROM shares
            WHERE COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) >= @MinimumPercentage
              AND (@MaximumPercentage::numeric IS NULL
                   OR COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) <= @MaximumPercentage::numeric);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var items = await connection.QueryAsync<PollingStationVoteConcentration>(
            new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken));

        var count = await connection.QuerySingleAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return new PaginatedList<PollingStationVoteConcentration>(items.AsList(), count, page, pageSize);
    }

    public async Task<IReadOnlyList<PollingStationVoteConcentration>> RankAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("Limit", limit);

        var sql = $"""
            {Cte},
            ranked AS (
                SELECT * FROM shares
                ORDER BY COALESCE(100.0 * "CandidateVotes" / NULLIF("ValidVotes", 0), 0) DESC,
                         "CandidateVotes" DESC,
                         "StateAbbreviation", "MunicipalityName", "TseZoneNumber", "TseSectionNumber"
                LIMIT @Limit
            )
            {string.Format(Projection, "ranked")}
            {Ordering};
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PollingStationVoteConcentration>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
