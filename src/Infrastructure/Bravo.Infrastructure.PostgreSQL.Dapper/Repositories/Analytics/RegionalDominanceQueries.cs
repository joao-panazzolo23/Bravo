using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Municipal-level dominance of one candidate, rolled up per state.
/// </summary>
/// <remarks>
/// A municipality is won when the candidate's nominal votes are strictly greater than every rival's,
/// so a tie is never reported as a win. Municipalities are rolled up only after the comparison, and
/// the candidate's share inside a municipality is computed against that municipality's valid votes.
///
/// The contest (year, election, office, round) is fixed by the filter, so it is projected as a
/// single-row CTE and crossed in after the state aggregation instead of joining it inside the
/// GROUP BY, where its columns would have to be aggregated.
///
/// The average winning margin only considers municipalities the candidate actually won: averaging a
/// margin across municipalities where the candidate lost would describe the opponent's victory, not
/// this candidacy's performance.
/// </remarks>
internal sealed class RegionalDominanceQueries(NpgsqlDataSource dataSource)
{
    private static string Sql => $"""
        WITH {AnalyticsScopeSql.TargetCandidates}
        {AnalyticsScopeSql.ScopedResults}
        contest AS (
            SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round"
            FROM scoped
            LIMIT 1
        ),
        candidate_info AS (
            SELECT CASE WHEN (SELECT COUNT(*) FROM target_candidates) = 1
                        THEN (SELECT target."CandidateId" FROM target_candidates AS target)
                        ELSE NULL::uuid END AS "CandidateId",
                   CASE WHEN (SELECT COUNT(*) FROM target_candidates) = 1
                        THEN (SELECT target."CandidateIdentityId" FROM target_candidates AS target)
                        ELSE NULL::uuid END AS "CandidateIdentityId",
                   CASE WHEN (SELECT COUNT(*) FROM target_candidates) = 1
                        THEN (SELECT candidate_row."Name"
                              FROM target_candidates AS target
                              INNER JOIN "Candidates" AS candidate_row ON candidate_row."Id" = target."CandidateId")
                        ELSE NULL::varchar END AS "CandidateName"
        ),
        municipality_valid AS (
            SELECT scoped."MunicipalityId", scoped."StateId", scoped."StateAbbreviation",
                   SUM(scoped."Votes")::bigint AS "ValidVotes"
            FROM scoped
            WHERE scoped."VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
            GROUP BY scoped."MunicipalityId", scoped."StateId", scoped."StateAbbreviation"
        ),
        candidate_municipality AS (
            SELECT scoped."MunicipalityId", scoped."CandidateId", SUM(scoped."Votes")::bigint AS "Votes"
            FROM scoped
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
            GROUP BY scoped."MunicipalityId", scoped."CandidateId"
        ),
        target_municipality AS (
            SELECT candidate_municipality."MunicipalityId",
                   SUM(candidate_municipality."Votes")::bigint AS "Votes"
            FROM candidate_municipality
            INNER JOIN target_candidates AS target
                ON target."CandidateId" = candidate_municipality."CandidateId"
            GROUP BY candidate_municipality."MunicipalityId"
        ),
        rival_municipality AS (
            SELECT candidate_municipality."MunicipalityId",
                   MAX(candidate_municipality."Votes") AS "BestVotes"
            FROM candidate_municipality
            WHERE NOT EXISTS (
                SELECT 1 FROM target_candidates AS target
                WHERE target."CandidateId" = candidate_municipality."CandidateId")
            GROUP BY candidate_municipality."MunicipalityId"
        ),
        per_municipality AS (
            SELECT valid."MunicipalityId", valid."StateId", valid."StateAbbreviation",
                   valid."ValidVotes",
                   COALESCE(target."Votes", 0)::bigint AS "TargetVotes",
                   COALESCE(rival."BestVotes", 0)::bigint AS "BestRivalVotes",
                   CASE WHEN valid."ValidVotes" > 0
                        THEN 100.0 * COALESCE(target."Votes", 0) / valid."ValidVotes"
                        ELSE NULL END AS "Percentage"
            FROM municipality_valid AS valid
            LEFT JOIN target_municipality AS target
                ON target."MunicipalityId" = valid."MunicipalityId"
            LEFT JOIN rival_municipality AS rival
                ON rival."MunicipalityId" = valid."MunicipalityId"
        ),
        state_totals AS (
            SELECT per_municipality."StateId", per_municipality."StateAbbreviation",
                   SUM(per_municipality."TargetVotes")::bigint AS "TotalVotes",
                   SUM(per_municipality."ValidVotes")::bigint AS "ValidVotes",
                   COUNT(*) FILTER (WHERE per_municipality."ValidVotes" > 0)::int AS "MunicipalitiesContested",
                   COUNT(*) FILTER (WHERE per_municipality."TargetVotes" > 0
                                      AND per_municipality."TargetVotes" > per_municipality."BestRivalVotes")::int AS "MunicipalitiesWon",
                   ROUND(AVG(per_municipality."Percentage")::numeric, 2) AS "AverageMunicipalityPercentage",
                   ROUND(MAX(per_municipality."Percentage")::numeric, 2) AS "HighestMunicipalityPercentage",
                   ROUND(MIN(per_municipality."Percentage")::numeric, 2) AS "LowestMunicipalityPercentage",
                   ROUND(AVG(CASE WHEN per_municipality."TargetVotes" > per_municipality."BestRivalVotes"
                                  AND per_municipality."TargetVotes" > 0
                                  AND per_municipality."ValidVotes" > 0
                             THEN 100.0 * (per_municipality."TargetVotes" - per_municipality."BestRivalVotes")
                                  / per_municipality."ValidVotes"
                             ELSE NULL END)::numeric, 2) AS "AverageWinningMarginPercentagePoints"
            FROM per_municipality
            WHERE (@StateId::uuid IS NULL OR per_municipality."StateId" = @StateId::uuid)
            GROUP BY per_municipality."StateId", per_municipality."StateAbbreviation"
            HAVING SUM(per_municipality."ValidVotes") > 0
        )
        SELECT contest."Year" AS "Year",
               contest."ElectionId" AS "ElectionId",
               contest."OfficeName" AS "OfficeName",
               contest."Round" AS "Round",
               state_totals."StateId" AS "StateId",
               state_totals."StateAbbreviation" AS "StateAbbreviation",
               candidate_info."CandidateId" AS "CandidateId",
               candidate_info."CandidateName" AS "CandidateName",
               candidate_info."CandidateIdentityId" AS "CandidateIdentityId",
               state_totals."TotalVotes" AS "TotalVotes",
               state_totals."ValidVotes" AS "ValidVotes",
               COALESCE(ROUND((100.0 * state_totals."TotalVotes"
                               / NULLIF(state_totals."ValidVotes", 0))::numeric, 2), 0) AS "VotePercentage",
               state_totals."MunicipalitiesContested" AS "MunicipalitiesContested",
               state_totals."MunicipalitiesWon" AS "MunicipalitiesWon",
               COALESCE(ROUND((100.0 * state_totals."MunicipalitiesWon"
                               / NULLIF(state_totals."MunicipalitiesContested", 0))::numeric, 2), 0) AS "MunicipalitiesWonPercentage",
               state_totals."AverageMunicipalityPercentage" AS "AverageMunicipalityPercentage",
               state_totals."HighestMunicipalityPercentage" AS "HighestMunicipalityPercentage",
               state_totals."LowestMunicipalityPercentage" AS "LowestMunicipalityPercentage",
               state_totals."AverageWinningMarginPercentagePoints" AS "AverageWinningMarginPercentagePoints"
        FROM state_totals
        CROSS JOIN contest
        CROSS JOIN candidate_info
        ORDER BY "VotePercentage" DESC, state_totals."StateAbbreviation"
        LIMIT @Limit;
        """;

    public async Task<IReadOnlyList<StateElectoralDominance>> GetAsync(
        ElectionAnalyticsFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("Limit", limit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<StateElectoralDominance>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
