using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Vote composition aggregated at one geographic level.
/// </summary>
/// <remarks>
/// Every number is a sum of stored <c>ElectionResults</c> rows for the active dataset version:
/// votes cast are all vote types, valid votes are nominal plus party votes, and blank and null are
/// counted from their own rows. There is no eligible-voter source in the schema
/// (<c>ElectionStatistics</c> was dropped), so attendance, abstention and turnout are projected as
/// <c>NULL</c> rather than inferred from the votes. Blank and null shares use votes cast as their
/// denominator, which is measurable.
///
/// The grouping keys are passed as a single list so a level only exposes the columns it actually
/// aggregates on; selecting a municipality for a state-level row would break the GROUP BY.
/// </remarks>
internal sealed class TurnoutCompositionQueries(NpgsqlDataSource dataSource)
{
    private static string GroupBy(VoteCompositionLevel level) => level switch
    {
        VoteCompositionLevel.PollingStation => """
            "StateId", "StateAbbreviation", "MunicipalityId", "MunicipalityName",
            "ZoneId", "TseZoneNumber", "SectionId", "TseSectionNumber"
            """,
        VoteCompositionLevel.Municipality => """
            "StateId", "StateAbbreviation", "MunicipalityId", "MunicipalityName"
            """,
        VoteCompositionLevel.ElectoralZone => """
            "StateId", "StateAbbreviation", "ZoneId", "TseZoneNumber"
            """,
        _ => """
            "StateId", "StateAbbreviation"
            """
    };

    private static string MunicipalityId(VoteCompositionLevel level) => level == VoteCompositionLevel.State
        ? "NULL::uuid"
        : "composition.\"MunicipalityId\"";

    private static string MunicipalityName(VoteCompositionLevel level)
        => level is VoteCompositionLevel.Municipality or VoteCompositionLevel.PollingStation
            ? "composition.\"MunicipalityName\""
            : "NULL::varchar";

    private static string ZoneId(VoteCompositionLevel level)
        => level is VoteCompositionLevel.ElectoralZone or VoteCompositionLevel.PollingStation
            ? "composition.\"ZoneId\""
            : "NULL::uuid";

    private static string ZoneNumber(VoteCompositionLevel level)
        => level is VoteCompositionLevel.ElectoralZone or VoteCompositionLevel.PollingStation
            ? "composition.\"TseZoneNumber\""
            : "NULL::int";

    private static string SectionId(VoteCompositionLevel level)
        => level == VoteCompositionLevel.PollingStation
            ? "composition.\"SectionId\""
            : "NULL::uuid";

    private static string SectionNumber(VoteCompositionLevel level)
        => level == VoteCompositionLevel.PollingStation
            ? "composition.\"TseSectionNumber\""
            : "NULL::int";

    private static string Sql(VoteCompositionLevel level) => $"""
        WITH {AnalyticsScopeSql.ScopedResults}
        contest AS (
            SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round"
            FROM scoped
            LIMIT 1
        ),
        composition AS (
            SELECT {GroupBy(level)},
                   SUM("Votes")::bigint AS "VotesCast",
                   (SUM("Votes") FILTER (WHERE "VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})))::bigint AS "ValidVotes",
                   (SUM("Votes") FILTER (WHERE "VoteType" = {AnalyticsScopeSql.BlankVoteType}))::bigint AS "BlankVotes",
                   (SUM("Votes") FILTER (WHERE "VoteType" = {AnalyticsScopeSql.NullVoteType}))::bigint AS "NullVotes"
            FROM scoped
            GROUP BY {GroupBy(level)}
        )
        SELECT contest."Year" AS "Year",
               contest."ElectionId" AS "ElectionId",
               contest."OfficeName" AS "OfficeName",
               contest."Round" AS "Round",
               @Level::int AS "Level",
               composition."StateId" AS "StateId",
               composition."StateAbbreviation" AS "StateAbbreviation",
               {MunicipalityId(level)} AS "MunicipalityId",
               {MunicipalityName(level)} AS "MunicipalityName",
               {ZoneId(level)} AS "ZoneId",
               {ZoneNumber(level)} AS "TseZoneNumber",
               {SectionId(level)} AS "SectionId",
               {SectionNumber(level)} AS "TseSectionNumber",
               composition."VotesCast" AS "VotesCast",
               COALESCE(composition."ValidVotes", 0) AS "ValidVotes",
               COALESCE(composition."BlankVotes", 0) AS "BlankVotes",
               COALESCE(composition."NullVotes", 0) AS "NullVotes",
               NULL::bigint AS "EligibleVoters",
               NULL::bigint AS "AttendingVoters",
               NULL::bigint AS "AbsenteeVoters",
               NULL::numeric AS "TurnoutPercentage",
               NULL::numeric AS "AbsenteeismPercentage",
               COALESCE(ROUND((100.0 * composition."BlankVotes" / NULLIF(composition."VotesCast", 0))::numeric, 2), 0) AS "BlankVotePercentage",
               COALESCE(ROUND((100.0 * composition."NullVotes" / NULLIF(composition."VotesCast", 0))::numeric, 2), 0) AS "NullVotePercentage"
        FROM composition
        CROSS JOIN contest
        ORDER BY composition."VotesCast" DESC, composition."StateAbbreviation"
        LIMIT @Limit;
        """;

    public async Task<IReadOnlyList<TurnoutComposition>> GetAsync(
        ElectionAnalyticsFilter filter,
        VoteCompositionLevel level,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("Level", (int)level);
        parameters.Add("Limit", limit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<TurnoutComposition>(
            new CommandDefinition(Sql(level), parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
