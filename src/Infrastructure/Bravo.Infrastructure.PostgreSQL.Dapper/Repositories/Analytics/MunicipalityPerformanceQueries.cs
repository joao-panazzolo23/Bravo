using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Performance of one candidate in every municipality of a contest.
/// </summary>
/// <remarks>
/// Votes and valid votes are aggregated per municipality from the scoped section rows, so a
/// municipality with several sections cannot be counted once per section.
///
/// The rank comes from a window over every candidate's municipality total in the same round and
/// office. A window is used instead of counting "candidates with more votes" so ties share a rank
/// and the query stays a single pass. <c>RANK()</c> returns <c>bigint</c> in PostgreSQL, so it is
/// narrowed to the <c>int</c> the contract declares.
/// </remarks>
internal sealed class MunicipalityPerformanceQueries(NpgsqlDataSource dataSource)
{
    private static string Ordering(MunicipalityPerformanceSort sort) => sort == MunicipalityPerformanceSort.VoteCount
        ? "ORDER BY \"Votes\" DESC, \"VotePercentage\" DESC, \"StateAbbreviation\", \"MunicipalityName\""
        : "ORDER BY \"VotePercentage\" DESC, \"Votes\" DESC, \"StateAbbreviation\", \"MunicipalityName\"";

    private static string Sql(MunicipalityPerformanceSort sort) => $"""
        WITH {AnalyticsScopeSql.TargetCandidates}
        {AnalyticsScopeSql.ScopedResults}
        contest AS (
            SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round"
            FROM scoped
            LIMIT 1
        ),
        municipality_valid AS (
            SELECT scoped."MunicipalityId", SUM(scoped."Votes")::bigint AS "ValidVotes"
            FROM scoped
            WHERE scoped."VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
            GROUP BY scoped."MunicipalityId"
        ),
        candidate_municipality AS (
            SELECT scoped."MunicipalityId", scoped."CandidateId", SUM(scoped."Votes")::bigint AS "Votes"
            FROM scoped
            WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
            GROUP BY scoped."MunicipalityId", scoped."CandidateId"
        ),
        ranked AS (
            SELECT candidate_municipality."MunicipalityId",
                   candidate_municipality."CandidateId",
                   candidate_municipality."Votes",
                   RANK() OVER (
                       PARTITION BY candidate_municipality."MunicipalityId"
                       ORDER BY candidate_municipality."Votes" DESC, candidate_municipality."CandidateId"
                   )::int AS "RankAmongCandidates"
            FROM candidate_municipality
        )
        SELECT contest."Year" AS "Year",
               contest."ElectionId" AS "ElectionId",
               contest."OfficeName" AS "OfficeName",
               contest."Round" AS "Round",
               municipality."Id" AS "MunicipalityId",
               municipality."Name" AS "MunicipalityName",
               state."Id" AS "StateId",
               state."Abbreviation" AS "StateAbbreviation",
               candidate."Id" AS "CandidateId",
               candidate."Name" AS "CandidateName",
               candidate."CandidateIdentityId" AS "CandidateIdentityId",
               identity."FullName" AS "CandidateIdentityName",
               ranked."Votes" AS "Votes",
               valid."ValidVotes" AS "ValidVotes",
               COALESCE(ROUND((100.0 * ranked."Votes" / NULLIF(valid."ValidVotes", 0))::numeric, 2), 0) AS "VotePercentage",
               ranked."RankAmongCandidates" AS "RankAmongCandidates"
        FROM ranked
        INNER JOIN target_candidates AS target ON target."CandidateId" = ranked."CandidateId"
        INNER JOIN "Candidates" AS candidate ON candidate."Id" = ranked."CandidateId"
        LEFT JOIN "CandidateIdentities" AS identity ON identity."Id" = candidate."CandidateIdentityId"
        INNER JOIN "Municipalities" AS municipality ON municipality."Id" = ranked."MunicipalityId"
        INNER JOIN "States" AS state ON state."Id" = municipality."StateId"
        INNER JOIN municipality_valid AS valid ON valid."MunicipalityId" = ranked."MunicipalityId"
        CROSS JOIN contest
        {Ordering(sort)}
        LIMIT @Limit;
        """;

    public async Task<IReadOnlyList<MunicipalityCandidatePerformance>> GetAsync(
        ElectionAnalyticsFilter filter,
        MunicipalityPerformanceSort sort,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("Limit", limit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<MunicipalityCandidatePerformance>(
            new CommandDefinition(Sql(sort), parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
