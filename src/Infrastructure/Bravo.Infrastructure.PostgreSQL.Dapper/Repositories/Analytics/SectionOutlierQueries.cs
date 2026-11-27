using Bravo.Domain.Electoral.Analytics;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Sections whose candidate share deviates from a reference population.
/// </summary>
/// <remarks>
/// The score is a voter-weighted Z-score. A plain Z-score over unweighted section percentages would
/// let a section with three valid votes move the mean and the standard deviation as much as a
/// section with three thousand, which makes the deviation meaningless in the very tails an outlier
/// search cares about. Weighting by valid votes makes the reference distribution approximate the
/// population of voters instead of the population of sections.
///
/// The reference percentage is the ratio of summed votes, and its dispersion is the weighted
/// variance of each section's share around that ratio. The population needs at least two sections
/// with positive dispersion; otherwise the ratio is <c>NULL</c> instead of a division by zero or a
/// fabricated score.
///
/// A deviation is a statistical observation only. Nothing here identifies fraud or irregularity.
/// </remarks>
internal sealed class SectionOutlierQueries(NpgsqlDataSource dataSource)
{
    private static string ReferenceColumn(OutlierReferenceLevel level) => level switch
    {
        OutlierReferenceLevel.Municipality => "MunicipalityId",
        OutlierReferenceLevel.ElectoralZone => "ZoneId",
        _ => "StateId"
    };

    private static string Sql(OutlierReferenceLevel level)
    {
        var key = ReferenceColumn(level);

        return $"""
            WITH {AnalyticsScopeSql.TargetCandidates}
            {AnalyticsScopeSql.ScopedResults}
            contest AS (
                SELECT DISTINCT "Year", "ElectionId", "OfficeName", "Round"
                FROM scoped
                LIMIT 1
            ),
            section_valid AS (
                SELECT "SectionId", SUM("Votes")::bigint AS "ValidVotes"
                FROM scoped
                WHERE "VoteType" IN ({AnalyticsScopeSql.NominalVoteType}, {AnalyticsScopeSql.PartyVoteType})
                GROUP BY "SectionId"
            ),
            sections AS (
                SELECT DISTINCT "SectionId", "TseSectionNumber", "MunicipalityId", "MunicipalityName",
                       "StateId", "StateAbbreviation", "ZoneId", "TseZoneNumber"
                FROM scoped
            ),
            section_candidate AS (
                SELECT scoped."SectionId", SUM(scoped."Votes")::bigint AS "CandidateVotes"
                FROM scoped
                INNER JOIN target_candidates AS target ON target."CandidateId" = scoped."CandidateId"
                WHERE scoped."VoteType" = {AnalyticsScopeSql.NominalVoteType}
                GROUP BY scoped."SectionId"
            ),
            observations AS (
                SELECT sections.*,
                       sections."{key}" AS "ReferenceKey",
                       COALESCE(candidate."CandidateVotes", 0) AS "CandidateVotes",
                       COALESCE(valid."ValidVotes", 0) AS "ValidVotes",
                       COALESCE(100.0 * COALESCE(candidate."CandidateVotes", 0)
                                / NULLIF(valid."ValidVotes", 0), 0) AS "Percentage"
                FROM sections
                LEFT JOIN section_candidate AS candidate ON candidate."SectionId" = sections."SectionId"
                LEFT JOIN section_valid AS valid ON valid."SectionId" = sections."SectionId"
            ),
            reference_base AS (
                SELECT "ReferenceKey",
                       COUNT(*)::int AS "SectionCount",
                       SUM("ValidVotes")::bigint AS "ReferenceValidVotes",
                       SUM("CandidateVotes")::bigint AS "ReferenceVotes"
                FROM observations
                GROUP BY "ReferenceKey"
            ),
            reference AS (
                SELECT "ReferenceKey", "SectionCount", "ReferenceValidVotes", "ReferenceVotes",
                       100.0 * "ReferenceVotes" / NULLIF("ReferenceValidVotes", 0) AS "ReferencePercentage"
                FROM reference_base
            ),
            dispersion AS (
                SELECT observations."ReferenceKey",
                       CASE WHEN SUM(observations."ValidVotes") > 0
                            THEN SQRT(SUM(observations."ValidVotes"
                                          * POWER(observations."Percentage" - reference."ReferencePercentage", 2))
                                      / SUM(observations."ValidVotes"))
                            ELSE NULL END AS "StandardDeviation"
                FROM observations
                INNER JOIN reference ON reference."ReferenceKey" = observations."ReferenceKey"
                GROUP BY observations."ReferenceKey"
            ),
            statistics AS (
                SELECT reference.*, dispersion."StandardDeviation"
                FROM reference
                INNER JOIN dispersion ON dispersion."ReferenceKey" = reference."ReferenceKey"
            )
            SELECT contest."Year" AS "Year",
                   contest."ElectionId" AS "ElectionId",
                   contest."OfficeName" AS "OfficeName",
                   contest."Round" AS "Round",
                   @ReferenceLevel::int AS "ReferenceLevel",
                   observations."SectionId" AS "SectionId",
                   observations."TseSectionNumber" AS "TseSectionNumber",
                   observations."MunicipalityId" AS "MunicipalityId",
                   observations."MunicipalityName" AS "MunicipalityName",
                   observations."StateId" AS "StateId",
                   observations."StateAbbreviation" AS "StateAbbreviation",
                   observations."ZoneId" AS "ZoneId",
                   observations."TseZoneNumber" AS "TseZoneNumber",
                   CASE WHEN (SELECT COUNT(*) FROM target_candidates) = 1
                        THEN (SELECT target."CandidateId" FROM target_candidates AS target)
                        ELSE NULL END AS "CandidateId",
                   CASE WHEN (SELECT COUNT(*) FROM target_candidates) = 1
                        THEN (SELECT candidate."Name"
                              FROM target_candidates AS target
                              INNER JOIN "Candidates" AS candidate ON candidate."Id" = target."CandidateId")
                        ELSE NULL END AS "CandidateName",
                   observations."CandidateVotes" AS "SectionVotes",
                   observations."ValidVotes" AS "SectionValidVotes",
                   ROUND(observations."Percentage"::numeric, 2) AS "SectionPercentage",
                   statistics."ReferenceVotes" AS "ReferenceVotes",
                   statistics."ReferenceValidVotes" AS "ReferenceValidVotes",
                   COALESCE(ROUND(statistics."ReferencePercentage"::numeric, 2), 0) AS "ReferencePercentage",
                   COALESCE(ROUND(statistics."StandardDeviation"::numeric, 2), 0) AS "ReferenceStandardDeviation",
                   ROUND((observations."Percentage" - statistics."ReferencePercentage")::numeric, 2) AS "DeviationPercentagePoints",
                   CASE WHEN statistics."StandardDeviation" > 0
                        THEN ROUND(((observations."Percentage" - statistics."ReferencePercentage")
                                    / statistics."StandardDeviation")::numeric, 4)
                        ELSE NULL END AS "DeviationRatio",
                   statistics."SectionCount"::bigint AS "ReferenceSectionCount",
                   (statistics."SectionCount" >= 2
                    AND statistics."StandardDeviation" > 0
                    AND ABS(observations."Percentage" - statistics."ReferencePercentage")
                        >= @Sensitivity * statistics."StandardDeviation") AS "IsOutlier"
            FROM observations
            INNER JOIN statistics ON statistics."ReferenceKey" = observations."ReferenceKey"
            CROSS JOIN contest
            WHERE statistics."SectionCount" >= 2
              AND statistics."StandardDeviation" > 0
              AND ABS(observations."Percentage" - statistics."ReferencePercentage")
                  >= @Sensitivity * statistics."StandardDeviation"
            ORDER BY ABS(COALESCE(((observations."Percentage" - statistics."ReferencePercentage")
                                   / NULLIF(statistics."StandardDeviation", 0)), 0)) DESC
            LIMIT @Limit;
            """;
    }

    public async Task<IReadOnlyList<SectionVoteOutlier>> GetAsync(
        ElectionAnalyticsFilter filter,
        OutlierReferenceLevel referenceLevel,
        decimal sensitivity,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = AnalyticsScopeSql.ParametersOf(filter).ToDynamicParameters();
        parameters.Add("ReferenceLevel", (int)referenceLevel);
        parameters.Add("Sensitivity", sensitivity);
        parameters.Add("Limit", limit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SectionVoteOutlier>(
            new CommandDefinition(Sql(referenceLevel), parameters, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
