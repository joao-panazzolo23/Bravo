using Bravo.Domain.Electoral.Analytics;
using Bravo.Domain.Electoral.Enums;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// SQL shared by the analytical queries.
/// </summary>
/// <remarks>
/// Three rules live here once instead of in every statement:
/// <list type="bullet">
/// <item>Only active dataset versions are read. A re-import supersedes the previous version rather
/// than deleting it, so reading every version would double the votes.</item>
/// <item>Valid votes are nominal plus party votes. Blank and null ballots are counted separately
/// and never enter a candidate share.</item>
/// <item>Every optional filter is a <c>NULL</c>-tolerant predicate applied before aggregation.</item>
/// </list>
/// </remarks>
internal static class AnalyticsScopeSql
{
    public const int NominalVoteType = (int)VoteType.Nominal;
    public const int PartyVoteType = (int)VoteType.Party;
    public const int BlankVoteType = (int)VoteType.Blank;
    public const int NullVoteType = (int)VoteType.Null;

    public const string TargetCandidates = """
        target_candidates AS (
            SELECT candidate."Id" AS "CandidateId", candidate."CandidateIdentityId"
            FROM "Candidates" AS candidate
            INNER JOIN "Elections" AS candidate_election
                ON candidate_election."Id" = candidate."ElectionId"
            WHERE (@CandidateId::uuid IS NULL OR candidate."Id" = @CandidateId::uuid)
              AND (@CandidateIdentityId::uuid IS NULL OR candidate."CandidateIdentityId" = @CandidateIdentityId::uuid)
              AND (@ElectionId::uuid IS NULL OR candidate."ElectionId" = @ElectionId::uuid)
              AND (@Year::int IS NULL OR candidate_election."Year" = @Year::int)
        ),
        """;

    /// <summary>
    /// One row per stored section result with its electoral and geographic context.
    /// </summary>
    public const string ScopedResults = """
        scoped AS (
            SELECT er."SectionId", er."CandidateId", er."VoteType", er."Votes",
                   rd."ElectionId", rd."Round", rd."IsRunoff",
                   election."Year", office."Name" AS "OfficeName",
                   section."MunicipalityId", section."ZoneId", section."TseSectionNumber",
                   municipality."Name" AS "MunicipalityName", municipality."StateId",
                   state."Abbreviation" AS "StateAbbreviation",
                   zone."TseZoneNumber",
                   assignment."PollingPlaceId",
                   place."TsePollingPlaceNumber",
                   place."Name" AS "PollingPlaceName"
            FROM "ElectionResults" AS er
            INNER JOIN "DatasetVersions" AS version
                ON version."Id" = er."DatasetVersionId" AND version."IsActive"
            INNER JOIN "ElectionRounds" AS rd ON rd."Id" = er."ElectionRoundId"
            INNER JOIN "Elections" AS election ON election."Id" = rd."ElectionId"
            INNER JOIN "ElectoralOffices" AS office ON office."Id" = er."OfficeId"
            INNER JOIN "ElectoralSections" AS section ON section."Id" = er."SectionId"
            INNER JOIN "Municipalities" AS municipality ON municipality."Id" = section."MunicipalityId"
            INNER JOIN "States" AS state ON state."Id" = municipality."StateId"
            INNER JOIN "ElectoralZones" AS zone ON zone."Id" = section."ZoneId"
            LEFT JOIN "PollingPlaceSections" AS assignment
                ON assignment."ElectionRoundId" = er."ElectionRoundId"
               AND assignment."SectionId" = er."SectionId"
            LEFT JOIN "PollingPlaces" AS place ON place."Id" = assignment."PollingPlaceId"
            WHERE (@ElectionId::uuid IS NULL OR rd."ElectionId" = @ElectionId::uuid)
              AND (@Year::int IS NULL OR election."Year" = @Year::int)
              AND (@OfficeId::uuid IS NULL OR er."OfficeId" = @OfficeId::uuid)
              AND (@Round::smallint IS NULL OR rd."Round" = @Round::smallint)
              AND (@StateId::uuid IS NULL OR municipality."StateId" = @StateId::uuid)
              AND (@MunicipalityId::uuid IS NULL OR section."MunicipalityId" = @MunicipalityId::uuid)
              AND (@ZoneId::uuid IS NULL OR section."ZoneId" = @ZoneId::uuid)
        ),
        """;

    /// <summary>Valid votes of one section in one round and office.</summary>
    public const string SectionValidVotes = """
        section_valid AS (
            SELECT "SectionId", "ElectionId", "Round",
                   SUM("Votes") AS "ValidVotes"
            FROM scoped
            WHERE "VoteType" IN (1, 2)
            GROUP BY "SectionId", "ElectionId", "Round"
        ),
        """;

    /// <summary>Votes the filtered candidacies received, per section.</summary>
    public const string SectionTargetVotes = """
        section_target AS (
            SELECT scoped."SectionId", scoped."ElectionId", scoped."Round",
                   SUM(scoped."Votes") AS "CandidateVotes",
                   COUNT(DISTINCT scoped."CandidateId") AS "CandidateCount",
                   MIN(scoped."CandidateId") AS "CandidateId"
            FROM scoped
            INNER JOIN target_candidates AS target
                ON target."CandidateId" = scoped."CandidateId"
            WHERE scoped."VoteType" = 1
            GROUP BY scoped."SectionId", scoped."ElectionId", scoped."Round"
        ),
        """;

    /// <summary>Percentage of a vote count over a denominator, rounded and null-safe.</summary>
    public static string Percentage(string numerator, string denominator)
        => $"ROUND((100.0 * {numerator} / NULLIF({denominator}, 0))::numeric, 2)";

    /// <summary>Maps a filter to the parameter record every statement binds.</summary>
    public static AnalyticsScopeParameters ParametersOf(ElectionAnalyticsFilter filter)
        => new(
            filter.ElectionId,
            filter.Year,
            filter.OfficeId,
            filter.Round,
            filter.StateId,
            filter.MunicipalityId,
            filter.ZoneId,
            filter.CandidateId,
            filter.CandidateIdentityId);

    /// <summary>Guards page inputs before they reach <c>OFFSET</c>.</summary>
    public static void ValidatePaging(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);
    }

    public static void ValidateLimit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 10000);
    }
}
