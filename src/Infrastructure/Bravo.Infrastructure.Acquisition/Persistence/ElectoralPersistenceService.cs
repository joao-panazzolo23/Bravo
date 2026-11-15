using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Services;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.Acquisition.Persistence;

/// <summary>
/// Set-based persistence of the electoral reference model. Elections, rounds, contests and
/// parties are small and are upserted with explicit SQL; candidates are staged with binary
/// COPY and merged with one statement per batch.
/// </summary>
public sealed class ElectoralPersistenceService(NpgsqlDataSource dataSource)
    : BulkPersistenceSession(dataSource), IElectoralPersistenceService
{
    private const string CandidatesStaging = "staging_candidates";

    private static readonly string[] CandidateColumns =
    [
        "election_id", "tse_sequential", "number", "name", "ballot_name", "office_code",
        "office_name", "party_id", "state_id", "situation_code", "situation_description", "birth_date"
    ];

    protected override string StagingDdl => """
        CREATE TEMP TABLE IF NOT EXISTS staging_candidates (
            election_id uuid NOT NULL,
            tse_sequential text NOT NULL,
            number text NOT NULL,
            name text NOT NULL,
            ballot_name text NOT NULL,
            office_code integer NOT NULL,
            office_name text NOT NULL,
            party_id uuid NULL,
            state_id uuid NULL,
            situation_code integer NULL,
            situation_description text NULL,
            birth_date date NULL
        ) ON COMMIT DELETE ROWS;
        """;

    private const string MergeCandidatesSql = """
        WITH source AS (
            SELECT DISTINCT ON (c.election_id, c.tse_sequential)
                   c.election_id, c.tse_sequential, c.number, c.name, c.ballot_name,
                   c.office_code, c.office_name, c.party_id, c.state_id,
                   c.situation_code, c.situation_description, c.birth_date
            FROM staging_candidates c
            ORDER BY c.election_id, c.tse_sequential, c.name
        ),
        merged AS (
            INSERT INTO "Candidates"
                ("Id", "ElectionId", "TseSequential", "Number", "Name", "BallotName", "OfficeCode",
                 "OfficeName", "PoliticalPartyId", "StateId", "SituationCode", "SituationDescription",
                 "BirthDate", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), source.election_id, source.tse_sequential, source.number, source.name,
                   source.ballot_name, source.office_code, source.office_name, source.party_id, source.state_id,
                   source.situation_code, source.situation_description, source.birth_date, now(), now()
            FROM source
            ON CONFLICT ("ElectionId", "TseSequential") DO UPDATE
            SET "Number" = EXCLUDED."Number",
                "Name" = EXCLUDED."Name",
                "BallotName" = EXCLUDED."BallotName",
                "OfficeCode" = EXCLUDED."OfficeCode",
                "OfficeName" = EXCLUDED."OfficeName",
                "PoliticalPartyId" = EXCLUDED."PoliticalPartyId",
                "StateId" = EXCLUDED."StateId",
                "SituationCode" = EXCLUDED."SituationCode",
                "SituationDescription" = EXCLUDED."SituationDescription",
                "BirthDate" = EXCLUDED."BirthDate",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM source)::int AS processed,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    public async Task<Guid> EnsureElection(
        string tseCode,
        int year,
        int typeCode,
        string typeName,
        string name,
        DateTime electionDate,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO "Elections" ("Id", "TseCode", "Year", "TypeCode", "TypeName", "Name", "ElectionDate", "CreatedAt", "UpdatedAt")
            VALUES (gen_random_uuid(), @TseCode, @Year, @TypeCode, @TypeName, @Name, @ElectionDate, now(), now())
            ON CONFLICT ("TseCode") DO UPDATE
            SET "TypeCode" = EXCLUDED."TypeCode",
                "TypeName" = EXCLUDED."TypeName",
                "Name" = EXCLUDED."Name",
                "ElectionDate" = EXCLUDED."ElectionDate",
                "UpdatedAt" = now()
            RETURNING "Id";
            """;

        var connection = await GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(Command(sql, new
        {
            TseCode = tseCode.Trim(),
            Year = year,
            TypeCode = typeCode,
            TypeName = typeName,
            Name = name,
            ElectionDate = electionDate
        }, ct));
    }

    public async Task<Guid> EnsureElectionRound(Guid electionId, int number, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO "ElectionRounds" ("Id", "ElectionId", "Number", "CreatedAt", "UpdatedAt")
            VALUES (gen_random_uuid(), @ElectionId, @Number, now(), now())
            ON CONFLICT ("ElectionId", "Number") DO UPDATE
            SET "UpdatedAt" = now()
            RETURNING "Id";
            """;

        var connection = await GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<Guid>(Command(sql, new
        {
            ElectionId = electionId,
            Number = number
        }, ct));
    }

    public async Task<Guid> EnsureContest(
        Guid electionRoundId,
        int officeCode,
        string officeName,
        string? stateAbbreviation,
        CancellationToken ct = default)
    {
        const string sql = """
            WITH state AS (
                SELECT "Id" FROM "States" WHERE "Abbreviation" = @StateAbbreviation LIMIT 1
            ),
            upsert AS (
                INSERT INTO "ElectoralContests" ("Id", "ElectionRoundId", "OfficeCode", "OfficeName", "StateId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), @ElectionRoundId, @OfficeCode, @OfficeName, (SELECT "Id" FROM state), now(), now()
                WHERE @StateAbbreviation IS NULL OR EXISTS (SELECT 1 FROM state)
                ON CONFLICT ("ElectionRoundId", "OfficeCode", "StateId") DO UPDATE
                SET "OfficeName" = EXCLUDED."OfficeName",
                    "UpdatedAt" = now()
                RETURNING "Id"
            )
            SELECT "Id" FROM upsert
            UNION ALL
            SELECT "Id" FROM "ElectoralContests"
            WHERE "ElectionRoundId" = @ElectionRoundId
              AND "OfficeCode" = @OfficeCode
              AND "StateId" IS NOT DISTINCT FROM (SELECT "Id" FROM state)
            LIMIT 1;
            """;

        var connection = await GetConnectionAsync(ct);
        return await connection.QueryFirstAsync<Guid>(Command(sql, new
        {
            ElectionRoundId = electionRoundId,
            OfficeCode = officeCode,
            OfficeName = officeName,
            StateAbbreviation = string.IsNullOrWhiteSpace(stateAbbreviation) ? null : stateAbbreviation.ToUpperInvariant()
        }, ct));
    }

    public async Task<IReadOnlyDictionary<int, Guid>> EnsureParties(
        IReadOnlyList<PartyReference> parties,
        CancellationToken ct = default)
    {
        if (parties.Count == 0)
        {
            return new Dictionary<int, Guid>();
        }

        const string sql = """
            INSERT INTO "PoliticalParties" ("Id", "Number", "Acronym", "Name", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), p."Number", p."Acronym", p."Name", now(), now()
            FROM unnest(@Numbers, @Acronyms, @Names) AS p("Number", "Acronym", "Name")
            ON CONFLICT ("Number") DO UPDATE
            SET "Acronym" = EXCLUDED."Acronym",
                "Name" = EXCLUDED."Name",
                "UpdatedAt" = now();
            """;

        var distinct = parties
            .GroupBy(p => p.Number)
            .Select(g => g.First())
            .OrderBy(p => p.Number)
            .ToList();

        var connection = await GetConnectionAsync(ct);
        await connection.ExecuteAsync(Command(sql, new
        {
            Numbers = distinct.Select(p => p.Number).ToArray(),
            Acronyms = distinct.Select(p => p.Acronym).ToArray(),
            Names = distinct.Select(p => p.Name).ToArray()
        }, ct));

        var lookup = await connection.QueryAsync<(int Number, Guid Id)>(Command(
            "SELECT \"Number\", \"Id\" FROM \"PoliticalParties\" WHERE \"Number\" = ANY(@Numbers);",
            new { Numbers = distinct.Select(p => p.Number).ToArray() },
            ct));

        return lookup.ToDictionary(x => x.Number, x => x.Id);
    }

    public Task<PersistenceResult> UpsertCandidates(
        Guid electionId,
        IReadOnlyList<CandidateReferenceRecord> batch,
        IReadOnlyDictionary<int, Guid> partyIds,
        IReadOnlyDictionary<string, Guid> stateIds,
        CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(PersistenceResult.Empty);
        }

        var rows = new List<CandidateRow>(batch.Count);
        var skipped = 0;

        foreach (var candidate in batch)
        {
            if (!partyIds.TryGetValue(candidate.PartyNumber, out var partyId))
            {
                skipped++;
                continue;
            }

            stateIds.TryGetValue(candidate.StateAbbreviation.ToUpperInvariant(), out var stateId);

            rows.Add(new CandidateRow(
                electionId,
                candidate.TseSequential,
                candidate.Number,
                candidate.Name,
                string.IsNullOrWhiteSpace(candidate.BallotName) ? candidate.Name : candidate.BallotName,
                candidate.OfficeCode,
                candidate.OfficeName,
                partyId,
                stateId == Guid.Empty ? null : stateId,
                candidate.SituationCode,
                candidate.SituationDescription,
                candidate.BirthDate));
        }

        if (rows.Count == 0)
        {
            return Task.FromResult(new PersistenceResult(0, 0, skipped));
        }

        return ExecuteBatchAsync(
            CandidatesStaging,
            CandidateColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Guid(importer, row.ElectionId, token);
                await BulkCopy.Text(importer, row.TseSequential, token);
                await BulkCopy.Text(importer, row.Number, token);
                await BulkCopy.Text(importer, row.Name, token);
                await BulkCopy.Text(importer, row.BallotName, token);
                await BulkCopy.Int(importer, row.OfficeCode, token);
                await BulkCopy.Text(importer, row.OfficeName, token);
                await BulkCopy.Guid(importer, row.PartyId, token);
                await BulkCopy.NullableGuid(importer, row.StateId, token);
                await BulkCopy.NullableInt(importer, row.SituationCode, token);
                await BulkCopy.NullableText(importer, row.SituationDescription, token);
                await BulkCopy.NullableDate(importer, row.BirthDate, token);
            },
            async (connection, transaction, token) =>
            {
                var result = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeCandidatesSql,
                    null,
                    token,
                    transaction));

                var unresolved = skipped + (rows.Count - result.Processed);
                return new PersistenceResult(result.Inserted, result.Processed - result.Inserted, unresolved);
            },
            ct);
    }

    public async Task<GeographicLookup> GetGeographicLookup(CancellationToken ct = default)
    {
        const string sql = """
            SELECT "Abbreviation", "Id" FROM "States";
            SELECT "TseCode", "MunicipalityId" FROM "MunicipalityTseCodes";
            """;

        var connection = await GetConnectionAsync(ct);
        await using var multi = await connection.QueryMultipleAsync(Command(sql, null, ct));
        var states = await multi.ReadAsync<(string Abbreviation, Guid Id)>();
        var municipalities = await multi.ReadAsync<(string TseCode, Guid MunicipalityId)>();

        return new GeographicLookup(
            states.ToDictionary(x => x.Abbreviation.ToUpperInvariant(), x => x.Id, StringComparer.OrdinalIgnoreCase),
            municipalities.ToDictionary(x => x.TseCode, x => x.MunicipalityId, StringComparer.Ordinal));
    }

    private sealed record CandidateRow(
        Guid ElectionId,
        string TseSequential,
        string Number,
        string Name,
        string BallotName,
        int OfficeCode,
        string OfficeName,
        Guid PartyId,
        Guid? StateId,
        int? SituationCode,
        string? SituationDescription,
        DateOnly? BirthDate);

    private sealed record MergeCounters(int Processed, int Inserted);
}
