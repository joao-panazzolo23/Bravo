using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Aggregated performance of one candidate or identity inside one election round.
/// </summary>
/// <remarks>
/// The CTE chain is the reference implementation for the other analyses: filter first, aggregate
/// votes per section, roll sections up to a municipality, then compare the target against its
/// rivals. Valid votes are nominal plus party votes and a municipality where the target received
/// nothing still contributes to the denominator, which is what keeps the overall share honest.
///
/// PostgreSQL widens <c>SUM(bigint)</c> to <c>numeric</c>, so every aggregate read back as a
/// <c>long</c> or <c>int</c> is cast explicitly. Without that cast Dapper asks for a decimal
/// constructor and the projection cannot be materialised.
/// </remarks>
internal sealed class ElectionPerformanceQueries(NpgsqlDataSource dataSource)
{
    private static string PerformanceSql => $"""
        WITH {AnalyticsScopeSql.TargetCandidates}
        {AnalyticsScopeSql.ScopedResults}
        municipality_valid AS (
            SELECT scoped."MunicipalityId",
                   SUM(scoped."Votes")::bigint AS "ValidVotes"
            FROM scoped
            WHERE scoped."VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
            GROUP BY scoped."MunicipalityId"
        ),
        target_votes AS (
            SELECT scoped."MunicipalityId", SUM(scoped."Votes")::bigint AS "Votes"
            FROM scoped
            INNER JOIN target_candidates AS target ON target."CandidateId" = scoped."CandidateId"
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
            GROUP BY scoped."MunicipalityId"
        ),
        rival_votes AS (
            SELECT scoped."MunicipalityId", scoped."CandidateId", SUM(scoped."Votes")::bigint AS "Votes"
            FROM scoped
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
              AND NOT EXISTS (
                  SELECT 1 FROM target_candidates AS target
                  WHERE target."CandidateId" = scoped."CandidateId")
            GROUP BY scoped."MunicipalityId", scoped."CandidateId"
        ),
        rival_best AS (
            SELECT rival_votes."MunicipalityId", MAX(rival_votes."Votes") AS "BestVotes"
            FROM rival_votes
            GROUP BY rival_votes."MunicipalityId"
        ),
        municipalities AS (
            SELECT valid."MunicipalityId", valid."ValidVotes",
                   COALESCE(target."Votes", 0)::bigint AS "TargetVotes",
                   COALESCE(rival."BestVotes", 0)::bigint AS "BestRivalVotes",
                   CASE WHEN valid."ValidVotes" > 0
                        THEN 100.0 * COALESCE(target."Votes", 0) / valid."ValidVotes"
                        ELSE NULL END AS "Percentage"
            FROM municipality_valid AS valid
            LEFT JOIN target_votes AS target ON target."MunicipalityId" = valid."MunicipalityId"
            LEFT JOIN rival_best AS rival ON rival."MunicipalityId" = valid."MunicipalityId"
        ),
        totals AS (
            SELECT COALESCE(SUM("Votes"), 0)::bigint AS "TotalVotes" FROM target_votes
        ),
        valid_total AS (
            SELECT COALESCE(SUM("ValidVotes"), 0)::bigint AS "ValidVotes" FROM municipality_valid
        ),
        election_row AS (
            SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round"
            FROM scoped
        ),
        resolved_candidate AS (
            SELECT target."CandidateId", target."CandidateIdentityId"
            FROM target_candidates AS target
            ORDER BY target."CandidateId"
            LIMIT 1
        )
        SELECT election_row."Year" AS "Year",
               election_row."ElectionId" AS "ElectionId",
               election_row."OfficeName" AS "OfficeName",
               election_row."Round" AS "Round",
               resolved_candidate."CandidateId" AS "CandidateId",
               candidate."Name" AS "CandidateName",
               resolved_candidate."CandidateIdentityId" AS "CandidateIdentityId",
               totals."TotalVotes" AS "TotalVotes",
               valid_total."ValidVotes" AS "ValidVotes",
               COALESCE({AnalyticsScopeSql.Percentage("totals.\"TotalVotes\"", "valid_total.\"ValidVotes\"")}, 0) AS "VotePercentage",
               (SELECT COUNT(*)::int FROM municipalities AS m WHERE m."TargetVotes" > 0) AS "MunicipalitiesWithVotes",
               (SELECT COUNT(*)::int FROM municipalities AS m
                WHERE m."TargetVotes" > 0 AND m."TargetVotes" > m."BestRivalVotes") AS "MunicipalitiesWon",
               (SELECT ROUND(AVG(m."Percentage")::numeric, 2) FROM municipalities AS m
                WHERE m."TargetVotes" > 0) AS "AverageMunicipalityPercentage",
               strongest."MunicipalityId" AS "StrongestMunicipalityId",
               strongest."Name" AS "StrongestMunicipalityName",
               strongest."Percentage" AS "StrongestMunicipalityPercentage"
        FROM election_row
        LEFT JOIN resolved_candidate ON TRUE
        LEFT JOIN "Candidates" AS candidate ON candidate."Id" = resolved_candidate."CandidateId"
        CROSS JOIN totals
        CROSS JOIN valid_total
        LEFT JOIN LATERAL (
            SELECT municipalities."MunicipalityId",
                   municipality."Name",
                   ROUND(municipalities."Percentage"::numeric, 2) AS "Percentage"
            FROM municipalities
            INNER JOIN "Municipalities" AS municipality
                ON municipality."Id" = municipalities."MunicipalityId"
            WHERE municipalities."TargetVotes" > 0
              AND municipalities."Percentage" IS NOT NULL
            ORDER BY municipalities."Percentage" DESC, municipalities."MunicipalityId"
            LIMIT 1
        ) AS strongest ON TRUE
        WHERE EXISTS (SELECT 1 FROM target_candidates)
        LIMIT 1;
        """;

    private static string MunicipalityStatsSql => $"""
        WITH {AnalyticsScopeSql.TargetCandidates}
        {AnalyticsScopeSql.ScopedResults}
        municipality_valid AS (
            SELECT scoped."MunicipalityId", SUM(scoped."Votes")::bigint AS "ValidVotes"
            FROM scoped
            WHERE scoped."VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
            GROUP BY scoped."MunicipalityId"
        ),
        target_votes AS (
            SELECT scoped."MunicipalityId", SUM(scoped."Votes")::bigint AS "Votes"
            FROM scoped
            INNER JOIN target_candidates AS target ON target."CandidateId" = scoped."CandidateId"
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
            GROUP BY scoped."MunicipalityId"
        ),
        rival_best AS (
            SELECT rivals."MunicipalityId", MAX(rivals."Votes") AS "BestVotes"
            FROM (
                SELECT scoped."MunicipalityId", scoped."CandidateId", SUM(scoped."Votes")::bigint AS "Votes"
                FROM scoped
                WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
                  AND NOT EXISTS (
                      SELECT 1 FROM target_candidates AS target
                      WHERE target."CandidateId" = scoped."CandidateId")
                GROUP BY scoped."MunicipalityId", scoped."CandidateId"
            ) AS rivals
            GROUP BY rivals."MunicipalityId"
        )
        SELECT valid."MunicipalityId" AS "MunicipalityId",
               valid."ValidVotes" AS "ValidVotes",
               COALESCE(target."Votes", 0)::bigint AS "TargetVotes",
               CASE WHEN valid."ValidVotes" > 0
                    THEN ROUND((100.0 * COALESCE(target."Votes", 0) / valid."ValidVotes")::numeric, 2)
                    ELSE NULL END AS "Percentage",
               (COALESCE(target."Votes", 0) > COALESCE(rival."BestVotes", 0)
                AND COALESCE(target."Votes", 0) > 0) AS "Won"
        FROM municipality_valid AS valid
        LEFT JOIN target_votes AS target ON target."MunicipalityId" = valid."MunicipalityId"
        LEFT JOIN rival_best AS rival ON rival."MunicipalityId" = valid."MunicipalityId";
        """;

    public async Task<HistoricalElectionPerformance?> GetPerformanceAsync(
        ElectionAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<HistoricalElectionPerformance>(
            new CommandDefinition(
                PerformanceSql,
                AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters(),
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MunicipalityContestStat>> GetMunicipalityStatsAsync(
        ElectionAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<MunicipalityContestStat>(
            new CommandDefinition(
                MunicipalityStatsSql,
                AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters(),
                cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
