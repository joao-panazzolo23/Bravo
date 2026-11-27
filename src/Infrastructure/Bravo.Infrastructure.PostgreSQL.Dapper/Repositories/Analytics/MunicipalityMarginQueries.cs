using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Distance between the two strongest candidates of each municipality.
/// </summary>
/// <remarks>
/// Candidates are aggregated per municipality first, then ranked with a window. Only ranks one and
/// two define the margin, so a municipality with many candidates is handled without assuming a
/// two-candidate contest.
///
/// An optional candidate filter narrows which municipalities are reported, not how the contest is
/// evaluated: the margin always belongs to the municipality's real top two. The filter matches a
/// municipality where the candidate finished first or second, which is the only situation where the
/// candidate's own margin is the reported margin.
/// </remarks>
internal sealed class MunicipalityMarginQueries(NpgsqlDataSource dataSource)
{
    private static string Ordering(MarginDirection direction) => direction == MarginDirection.Closest
        ? "ORDER BY \"MarginVotes\" ASC, \"MarginPercentagePoints\" ASC, \"StateAbbreviation\", \"MunicipalityName\""
        : "ORDER BY \"MarginVotes\" DESC, \"MarginPercentagePoints\" DESC, \"StateAbbreviation\", \"MunicipalityName\"";

    private static string Sql(MarginDirection direction) => $"""
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
                   )::int AS "Position"
            FROM candidate_municipality
        ),
        leader AS (SELECT * FROM ranked WHERE "Position" = 1),
        runner AS (SELECT * FROM ranked WHERE "Position" = 2),
        margins AS (
            SELECT leader."MunicipalityId",
                   leader."CandidateId" AS "WinnerCandidateId",
                   leader."Votes" AS "WinnerVotes",
                   runner."CandidateId" AS "RunnerUpCandidateId",
                   runner."Votes" AS "RunnerUpVotes",
                   valid."ValidVotes",
                   (leader."Votes" - runner."Votes") AS "MarginVotes",
                   CASE WHEN valid."ValidVotes" > 0
                        THEN ROUND((100.0 * (leader."Votes" - runner."Votes")
                                    / valid."ValidVotes")::numeric, 2)
                        ELSE 0 END AS "MarginPercentagePoints",
                   (leader."Votes" = runner."Votes") AS "IsTie"
            FROM leader
            INNER JOIN runner ON runner."MunicipalityId" = leader."MunicipalityId"
            INNER JOIN municipality_valid AS valid ON valid."MunicipalityId" = leader."MunicipalityId"
        )
        SELECT contest."Year" AS "Year",
               contest."ElectionId" AS "ElectionId",
               contest."OfficeName" AS "OfficeName",
               contest."Round" AS "Round",
               municipality."Id" AS "MunicipalityId",
               municipality."Name" AS "MunicipalityName",
               state."Id" AS "StateId",
               state."Abbreviation" AS "StateAbbreviation",
               margins."WinnerCandidateId" AS "WinnerCandidateId",
               winner."Name" AS "WinnerName",
               winner."PartyId" AS "WinnerPartyId",
               winner_party."Acronym" AS "WinnerPartyAcronym",
               margins."WinnerVotes" AS "WinnerVotes",
               COALESCE(ROUND((100.0 * margins."WinnerVotes" / NULLIF(margins."ValidVotes", 0))::numeric, 2), 0) AS "WinnerPercentage",
               margins."RunnerUpCandidateId" AS "RunnerUpCandidateId",
               runner_candidate."Name" AS "RunnerUpName",
               runner_candidate."PartyId" AS "RunnerUpPartyId",
               runner_party."Acronym" AS "RunnerUpPartyAcronym",
               margins."RunnerUpVotes" AS "RunnerUpVotes",
               COALESCE(ROUND((100.0 * margins."RunnerUpVotes" / NULLIF(margins."ValidVotes", 0))::numeric, 2), 0) AS "RunnerUpPercentage",
               margins."MarginVotes" AS "MarginVotes",
               margins."MarginPercentagePoints" AS "MarginPercentagePoints",
               margins."IsTie" AS "IsTie"
        FROM margins
        INNER JOIN "Candidates" AS winner ON winner."Id" = margins."WinnerCandidateId"
        
        LEFT JOIN "PoliticalParties" AS winner_party ON winner_party."Id" = winner."PartyId"
        INNER JOIN "Candidates" AS runner_candidate ON runner_candidate."Id" = margins."RunnerUpCandidateId"
        
        LEFT JOIN "PoliticalParties" AS runner_party ON runner_party."Id" = runner_candidate."PartyId"
        INNER JOIN "Municipalities" AS municipality ON municipality."Id" = margins."MunicipalityId"
        INNER JOIN "States" AS state ON state."Id" = municipality."StateId"
        CROSS JOIN contest
        WHERE (@StateId::uuid IS NULL OR municipality."StateId" = @StateId::uuid)
          AND (NOT EXISTS (SELECT 1 FROM target_candidates)
               OR EXISTS (
                   SELECT 1 FROM target_candidates AS target
                   WHERE target."CandidateId" IN (margins."WinnerCandidateId", margins."RunnerUpCandidateId")))
        {Ordering(direction)}
        LIMIT @Limit;
        """;

    public async Task<IReadOnlyList<MunicipalityElectoralMargin>> GetAsync(
        ElectionAnalyticsFilter filter,
        MarginDirection direction,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("Limit", limit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<MunicipalityElectoralMargin>(
            new CommandDefinition(Sql(direction), parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
