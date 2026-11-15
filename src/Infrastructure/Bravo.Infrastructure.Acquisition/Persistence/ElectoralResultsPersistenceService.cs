using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Services;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.Acquisition.Persistence;

/// <summary>
/// Bulk persistence of electoral facts.
///
/// A batch is fully staged with PostgreSQL binary COPY into session-local temporary tables.
/// One set-based statement then resolves geography references, creates missing zones, places
/// and sections, and merges the facts into their target tables. The whole batch runs inside a
/// single explicit transaction: either every row of the batch is visible or none is.
///
/// Rows whose state, municipality or candidate cannot be resolved are reported instead of
/// being silently dropped.
/// </summary>
public sealed class ElectoralResultsPersistenceService(NpgsqlDataSource dataSource)
    : BulkPersistenceSession(dataSource), IElectoralResultsPersistenceService
{
    private const string SectionsStaging = "staging_polling_sections";
    private const string ResultsStaging = "staging_polling_section_results";
    private const string StatisticsStaging = "staging_polling_section_statistics";

    private static readonly string[] SectionColumns =
    [
        "state_abbreviation", "municipality_tse_code", "zone_number", "section_number",
        "place_code", "place_name", "place_address"
    ];

    private static readonly string[] ResultColumns =
    [
        "state_abbreviation", "municipality_tse_code", "zone_number", "section_number",
        "office_code", "office_name", "candidate_sequential", "votable_number", "votable_name",
        "votes", "is_blank", "is_null"
    ];

    private static readonly string[] StatisticsColumns =
    [
        "state_abbreviation", "municipality_tse_code", "zone_number", "section_number",
        "office_code", "office_name", "eligible_voters", "attendees", "absentees",
        "nominal_votes", "blank_votes", "null_votes", "party_votes"
    ];

    protected override string StagingDdl => """
        CREATE TEMP TABLE IF NOT EXISTS staging_polling_sections (
            state_abbreviation text NOT NULL,
            municipality_tse_code text NOT NULL,
            zone_number text NOT NULL,
            section_number text NOT NULL,
            place_code text NOT NULL,
            place_name text NULL,
            place_address text NULL
        ) ON COMMIT DELETE ROWS;

        CREATE TEMP TABLE IF NOT EXISTS staging_polling_section_results (
            state_abbreviation text NOT NULL,
            municipality_tse_code text NOT NULL,
            zone_number text NOT NULL,
            section_number text NOT NULL,
            office_code integer NOT NULL,
            office_name text NOT NULL,
            candidate_sequential text NULL,
            votable_number text NOT NULL,
            votable_name text NOT NULL,
            votes integer NOT NULL,
            is_blank boolean NOT NULL,
            is_null boolean NOT NULL
        ) ON COMMIT DELETE ROWS;

        CREATE TEMP TABLE IF NOT EXISTS staging_polling_section_statistics (
            state_abbreviation text NOT NULL,
            municipality_tse_code text NOT NULL,
            zone_number text NOT NULL,
            section_number text NOT NULL,
            office_code integer NOT NULL,
            office_name text NOT NULL,
            eligible_voters integer NULL,
            attendees integer NULL,
            absentees integer NULL,
            nominal_votes integer NULL,
            blank_votes integer NULL,
            null_votes integer NULL,
            party_votes integer NULL
        ) ON COMMIT DELETE ROWS;
        """;

    /// <summary>
    /// Creates the geographic reference rows for the staged sections in the same transaction:
    /// zones, polling places and sections. Each step is a single set-based statement so the
    /// batch never issues one command per record. Sections are keyed by state, zone and number.
    /// </summary>
    private const string MergeZonesSql = """
        INSERT INTO "ElectoralZones" ("Id", "StateId", "Number", "CreatedAt", "UpdatedAt")
        SELECT gen_random_uuid(), st."Id", s.zone_number, now(), now()
        FROM staging_polling_sections s
        JOIN "States" st ON st."Abbreviation" = s.state_abbreviation
        GROUP BY st."Id", s.zone_number
        ON CONFLICT ("StateId", "Number") DO NOTHING;
        """;

    private const string MergePlacesSql = """
        INSERT INTO "PollingPlaces" ("Id", "MunicipalityId", "ZoneNumber", "TseCode", "Name", "Address", "CreatedAt", "UpdatedAt")
        SELECT gen_random_uuid(), mtc."MunicipalityId", s.zone_number, s.place_code,
               max(s.place_name), max(s.place_address), now(), now()
        FROM staging_polling_sections s
        JOIN "MunicipalityTseCodes" mtc ON mtc."TseCode" = s.municipality_tse_code
        GROUP BY mtc."MunicipalityId", s.zone_number, s.place_code
        ON CONFLICT ("MunicipalityId", "ZoneNumber", "TseCode") DO UPDATE
        SET "Name" = COALESCE(EXCLUDED."Name", "PollingPlaces"."Name"),
            "Address" = COALESCE(EXCLUDED."Address", "PollingPlaces"."Address"),
            "UpdatedAt" = now();
        """;

    private const string MergeSectionsSql = """
        WITH resolved AS (
            SELECT DISTINCT ON (st."Id", s.zone_number, s.section_number)
                   st."Id" AS state_id,
                   s.zone_number,
                   s.section_number,
                   s.place_code,
                   mtc."MunicipalityId" AS municipality_id
            FROM staging_polling_sections s
            JOIN "States" st ON st."Abbreviation" = s.state_abbreviation
            LEFT JOIN "MunicipalityTseCodes" mtc ON mtc."TseCode" = s.municipality_tse_code
            ORDER BY st."Id", s.zone_number, s.section_number
        ),
        merged AS (
            INSERT INTO "PollingSections" ("Id", "StateId", "ZoneNumber", "Number", "MunicipalityId", "PollingPlaceId", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), r.state_id, r.zone_number, r.section_number, r.municipality_id,
                   p."Id", now(), now()
            FROM resolved r
            LEFT JOIN "PollingPlaces" p
                   ON p."MunicipalityId" = r.municipality_id
                  AND p."ZoneNumber" = r.zone_number
                  AND p."TseCode" = r.place_code
            WHERE r.municipality_id IS NOT NULL
            ON CONFLICT ("StateId", "ZoneNumber", "Number") DO UPDATE
            SET "MunicipalityId" = EXCLUDED."MunicipalityId",
                "PollingPlaceId" = COALESCE(EXCLUDED."PollingPlaceId", "PollingSections"."PollingPlaceId"),
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM resolved)::int AS processed,
               (SELECT count(*) FROM resolved WHERE municipality_id IS NULL)::int AS unresolved,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    /// <summary>
    /// Merges candidate votes. Only rows with a resolvable candidate are inserted; blank and
    /// null votes are not candidate facts and are counted separately in the statistics table.
    /// </summary>
    private const string MergeResultsSql = """
        WITH resolved AS (
            SELECT r.*,
                   st."Id" AS state_id,
                   c."Id" AS candidate_id,
                   ct."Id" AS contest_id
            FROM staging_polling_section_results r
            JOIN "States" st ON st."Abbreviation" = r.state_abbreviation
            JOIN "CandidateLookup" c
                 ON c."TseSequential" = r.candidate_sequential
                AND c."ElectionId" = @ElectionId
            JOIN "ElectoralContests" ct
                 ON ct."ElectionRoundId" = @RoundId
                AND ct."OfficeCode" = r.office_code
                AND ct."StateId" IS NOT DISTINCT FROM CASE
                        WHEN r.office_code IN (1, 2) THEN NULL ELSE st."Id" END
            WHERE NOT r.is_blank AND NOT r.is_null
        ),
        facts AS (
            SELECT r.*,
                   sec."Id" AS section_id
            FROM resolved r
            JOIN "PollingSections" sec
                 ON sec."StateId" = r.state_id
                AND sec."ZoneNumber" = r.zone_number
                AND sec."Number" = r.section_number
        ),
        merged AS (
            INSERT INTO "PollingSectionResults"
                ("Id", "ElectionId", "ElectionRoundId", "ElectoralContestId", "PollingSectionId",
                 "CandidateId", "Votes", "ProvenanceId", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), @ElectionId, @RoundId, f.contest_id, f.section_id,
                   f.candidate_id, sum(f.votes), @ProvenanceId, now(), now()
            FROM facts f
            GROUP BY f.contest_id, f.section_id, f.candidate_id
            ON CONFLICT ("ElectoralContestId", "PollingSectionId", "CandidateId") DO UPDATE
            SET "Votes" = EXCLUDED."Votes",
                "ProvenanceId" = EXCLUDED."ProvenanceId",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM facts)::int AS facts,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    private const string MergeStatisticsSql = """
        WITH resolved AS (
            SELECT s.*,
                   st."Id" AS state_id,
                   ct."Id" AS contest_id
            FROM staging_polling_section_statistics s
            JOIN "States" st ON st."Abbreviation" = s.state_abbreviation
            JOIN "ElectoralContests" ct
                 ON ct."ElectionRoundId" = @RoundId
                AND ct."OfficeCode" = s.office_code
                AND ct."StateId" IS NOT DISTINCT FROM CASE
                        WHEN s.office_code IN (1, 2) THEN NULL ELSE st."Id" END
        ),
        facts AS (
            SELECT r.*,
                   sec."Id" AS section_id
            FROM resolved r
            JOIN "PollingSections" sec
                 ON sec."StateId" = r.state_id
                AND sec."ZoneNumber" = r.zone_number
                AND sec."Number" = r.section_number
        ),
        merged AS (
            INSERT INTO "PollingSectionStatistics"
                ("Id", "ElectionId", "ElectionRoundId", "ElectoralContestId", "PollingSectionId",
                 "EligibleVoters", "Attendees", "Absentees", "NominalVotes", "BlankVotes",
                 "NullVotes", "PartyVotes", "ProvenanceId", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), @ElectionId, @RoundId, f.contest_id, f.section_id,
                   max(f.eligible_voters), max(f.attendees), max(f.absentees), max(f.nominal_votes),
                   max(f.blank_votes), max(f.null_votes), max(f.party_votes), @ProvenanceId, now(), now()
            FROM facts f
            GROUP BY f.contest_id, f.section_id
            ON CONFLICT ("ElectoralContestId", "PollingSectionId") DO UPDATE
            SET "EligibleVoters" = EXCLUDED."EligibleVoters",
                "Attendees" = EXCLUDED."Attendees",
                "Absentees" = EXCLUDED."Absentees",
                "NominalVotes" = EXCLUDED."NominalVotes",
                "BlankVotes" = EXCLUDED."BlankVotes",
                "NullVotes" = EXCLUDED."NullVotes",
                "PartyVotes" = EXCLUDED."PartyVotes",
                "ProvenanceId" = EXCLUDED."ProvenanceId",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM facts)::int AS facts,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    public Task<PersistenceResult> UpsertPollingSections(
        IReadOnlyList<TsePollingSectionRecord> batch,
        CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(PersistenceResult.Empty);
        }

        var rows = batch
            .Where(s => !string.IsNullOrWhiteSpace(s.StateAbbreviation)
                        && !string.IsNullOrWhiteSpace(s.MunicipalityTseCode)
                        && !string.IsNullOrWhiteSpace(s.ZoneNumber)
                        && !string.IsNullOrWhiteSpace(s.SectionNumber)
                        && !string.IsNullOrWhiteSpace(s.PollingPlaceCode))
            .Select(s => new SectionRow(
                s.StateAbbreviation.ToUpperInvariant(),
                s.MunicipalityTseCode,
                s.ZoneNumber,
                s.SectionNumber,
                s.PollingPlaceCode,
                s.PollingPlaceName,
                s.PollingPlaceAddress))
            .ToList();

        if (rows.Count == 0)
        {
            return Task.FromResult(new PersistenceResult(0, 0, batch.Count));
        }

        return ExecuteBatchAsync(
            SectionsStaging,
            SectionColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Text(importer, row.StateAbbreviation, token);
                await BulkCopy.Text(importer, row.MunicipalityTseCode, token);
                await BulkCopy.Text(importer, row.ZoneNumber, token);
                await BulkCopy.Text(importer, row.SectionNumber, token);
                await BulkCopy.Text(importer, row.PlaceCode, token);
                await BulkCopy.NullableText(importer, row.PlaceName, token);
                await BulkCopy.NullableText(importer, row.PlaceAddress, token);
            },
            async (connection, transaction, token) =>
            {
                await connection.ExecuteAsync(Command(MergeZonesSql, null, token, transaction));
                await connection.ExecuteAsync(Command(MergePlacesSql, null, token, transaction));

                var counters = await connection.QuerySingleAsync<SectionCounters>(Command(
                    MergeSectionsSql,
                    null,
                    token,
                    transaction));

                var skipped = batch.Count - counters.Processed + counters.Unresolved;
                return new PersistenceResult(counters.Inserted, counters.Processed - counters.Inserted, skipped);
            },
            ct);
    }

    public Task<ResultMerge> UpsertPollingSectionResults(
        Guid electionId,
        int roundNumber,
        Guid provenanceId,
        IReadOnlyList<TsePollingSectionResultRecord> batch,
        CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(ResultMerge.Empty);
        }

        // Blank, null and party votes are not candidate facts; they are represented in the
        // statistics table (blank/null) or are intentionally outside the candidate fact set.
        var nonCandidateRows = batch.Count(r => r.IsBlank || r.IsNull
            || string.IsNullOrWhiteSpace(r.CandidateSequential));
        var candidateRows = batch
            .Where(r => !r.IsBlank && !r.IsNull)
            .Where(r => !string.IsNullOrWhiteSpace(r.CandidateSequential))
            .ToList();

        var rows = candidateRows
            .Where(r => !string.IsNullOrWhiteSpace(r.StateAbbreviation)
                        && !string.IsNullOrWhiteSpace(r.MunicipalityTseCode)
                        && !string.IsNullOrWhiteSpace(r.ZoneNumber)
                        && !string.IsNullOrWhiteSpace(r.SectionNumber)
                        && r.Votes >= 0)
            .Select(r => new ResultRow(
                r.StateAbbreviation.ToUpperInvariant(),
                r.MunicipalityTseCode,
                r.ZoneNumber,
                r.SectionNumber,
                r.OfficeCode,
                r.OfficeName,
                r.CandidateSequential!,
                r.VotableNumber,
                r.VotableName,
                r.Votes,
                r.IsBlank,
                r.IsNull))
            .ToList();

        var malformedRows = candidateRows.Count - rows.Count;

        if (rows.Count == 0)
        {
            return Task.FromResult(new ResultMerge(0, 0, malformedRows, []));
        }

        return ExecuteBatchAsync(
            ResultsStaging,
            ResultColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Text(importer, row.StateAbbreviation, token);
                await BulkCopy.Text(importer, row.MunicipalityTseCode, token);
                await BulkCopy.Text(importer, row.ZoneNumber, token);
                await BulkCopy.Text(importer, row.SectionNumber, token);
                await BulkCopy.Int(importer, row.OfficeCode, token);
                await BulkCopy.Text(importer, row.OfficeName, token);
                await BulkCopy.Text(importer, row.CandidateSequential, token);
                await BulkCopy.Text(importer, row.VotableNumber, token);
                await BulkCopy.Text(importer, row.VotableName, token);
                await BulkCopy.Int(importer, row.Votes, token);
                await BulkCopy.Bool(importer, row.IsBlank, token);
                await BulkCopy.Bool(importer, row.IsNull, token);
            },
            async (connection, transaction, token) =>
            {
                var roundId = await connection.ExecuteScalarAsync<Guid?>(Command(
                    "SELECT \"Id\" FROM \"ElectionRounds\" WHERE \"ElectionId\" = @ElectionId AND \"Number\" = @Round LIMIT 1;",
                    new { ElectionId = electionId, Round = roundNumber },
                    token,
                    transaction));

                if (roundId is null)
                {
                    return new ResultMerge(0, 0, rows.Count, ["Election round not found."]);
                }

                await CreateCandidateLookupAsync(connection, transaction, electionId, token);

                var counters = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeResultsSql,
                    new { ElectionId = electionId, RoundId = roundId.Value, ProvenanceId = provenanceId },
                    token,
                    transaction));

                var unresolved = rows.Count - counters.Facts;
                return new ResultMerge(
                    counters.Inserted,
                    counters.Facts - counters.Inserted,
                    unresolved + malformedRows,
                    unresolved > 0 ? [$"{unresolved} rows could not resolve candidate, contest or section."] : []);
            },
            ct);
    }

    public Task<ResultMerge> UpsertPollingSectionStatistics(
        Guid electionId,
        int roundNumber,
        Guid provenanceId,
        IReadOnlyList<TsePollingSectionStatisticsRecord> batch,
        CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(ResultMerge.Empty);
        }

        var rows = batch
            .Where(r => !string.IsNullOrWhiteSpace(r.StateAbbreviation)
                        && !string.IsNullOrWhiteSpace(r.MunicipalityTseCode)
                        && !string.IsNullOrWhiteSpace(r.ZoneNumber)
                        && !string.IsNullOrWhiteSpace(r.SectionNumber))
            .Select(r => new StatisticsRow(
                r.StateAbbreviation.ToUpperInvariant(),
                r.MunicipalityTseCode,
                r.ZoneNumber,
                r.SectionNumber,
                r.OfficeCode,
                r.OfficeName,
                r.EligibleVoters,
                r.Attendees,
                r.Absentees,
                r.NominalVotes,
                r.BlankVotes,
                r.NullVotes,
                r.PartyVotes))
            .ToList();

        if (rows.Count == 0)
        {
            return Task.FromResult(new ResultMerge(0, 0, batch.Count, []));
        }

        return ExecuteBatchAsync(
            StatisticsStaging,
            StatisticsColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Text(importer, row.StateAbbreviation, token);
                await BulkCopy.Text(importer, row.MunicipalityTseCode, token);
                await BulkCopy.Text(importer, row.ZoneNumber, token);
                await BulkCopy.Text(importer, row.SectionNumber, token);
                await BulkCopy.Int(importer, row.OfficeCode, token);
                await BulkCopy.Text(importer, row.OfficeName, token);
                await BulkCopy.NullableInt(importer, row.EligibleVoters, token);
                await BulkCopy.NullableInt(importer, row.Attendees, token);
                await BulkCopy.NullableInt(importer, row.Absentees, token);
                await BulkCopy.NullableInt(importer, row.NominalVotes, token);
                await BulkCopy.NullableInt(importer, row.BlankVotes, token);
                await BulkCopy.NullableInt(importer, row.NullVotes, token);
                await BulkCopy.NullableInt(importer, row.PartyVotes, token);
            },
            async (connection, transaction, token) =>
            {
                var roundId = await connection.ExecuteScalarAsync<Guid?>(Command(
                    "SELECT \"Id\" FROM \"ElectionRounds\" WHERE \"ElectionId\" = @ElectionId AND \"Number\" = @Round LIMIT 1;",
                    new { ElectionId = electionId, Round = roundNumber },
                    token,
                    transaction));

                if (roundId is null)
                {
                    return new ResultMerge(0, 0, rows.Count, ["Election round not found."]);
                }

                var counters = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeStatisticsSql,
                    new { ElectionId = electionId, RoundId = roundId.Value, ProvenanceId = provenanceId },
                    token,
                    transaction));

                var unresolved = rows.Count - counters.Facts;
                return new ResultMerge(
                    counters.Inserted,
                    counters.Facts - counters.Inserted,
                    unresolved + (batch.Count - rows.Count),
                    unresolved > 0 ? [$"{unresolved} statistics rows could not resolve contest or section."] : []);
            },
            ct);
    }

    /// <summary>
    /// Builds a session-local lookup of the candidates of one election. The merge joins this
    /// temporary table instead of correlating a subquery per staged row.
    /// </summary>
    private async Task CreateCandidateLookupAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid electionId,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(Command(
            """
            CREATE TEMP TABLE IF NOT EXISTS "CandidateLookup" (
                "TseSequential" text NOT NULL,
                "ElectionId" uuid NOT NULL,
                "Id" uuid NOT NULL,
                PRIMARY KEY ("ElectionId", "TseSequential")
            ) ON COMMIT DELETE ROWS;
            TRUNCATE "CandidateLookup";
            INSERT INTO "CandidateLookup" ("TseSequential", "ElectionId", "Id")
            SELECT "TseSequential", "ElectionId", "Id"
            FROM "Candidates"
            WHERE "ElectionId" = @ElectionId;
            """,
            new { ElectionId = electionId },
            ct,
            transaction));
    }

    private sealed record SectionRow(
        string StateAbbreviation,
        string MunicipalityTseCode,
        string ZoneNumber,
        string SectionNumber,
        string PlaceCode,
        string? PlaceName,
        string? PlaceAddress);

    private sealed record ResultRow(
        string StateAbbreviation,
        string MunicipalityTseCode,
        string ZoneNumber,
        string SectionNumber,
        int OfficeCode,
        string OfficeName,
        string CandidateSequential,
        string VotableNumber,
        string VotableName,
        int Votes,
        bool IsBlank,
        bool IsNull);

    private sealed record StatisticsRow(
        string StateAbbreviation,
        string MunicipalityTseCode,
        string ZoneNumber,
        string SectionNumber,
        int OfficeCode,
        string OfficeName,
        int? EligibleVoters,
        int? Attendees,
        int? Absentees,
        int? NominalVotes,
        int? BlankVotes,
        int? NullVotes,
        int? PartyVotes);

    private sealed record MergeCounters(int Facts, int Inserted);
    private sealed record SectionCounters(int Processed, int Unresolved, int Inserted);
}
