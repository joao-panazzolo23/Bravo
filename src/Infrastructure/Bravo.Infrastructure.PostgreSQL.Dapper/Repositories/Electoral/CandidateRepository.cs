using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Models;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

public sealed class CandidateRepository(NpgsqlDataSource dataSource) : ICandidateRepository
{
    // Candidates and identities used to be serialized to one JSON string per batch and parsed
    // back by jsonb_to_recordset. Both now stream into a temporary table with binary COPY and
    // merge with a single INSERT ... SELECT. Staging columns repeat the types the JSON casts
    // declared, so an oversized value still fails with the real database error (22001).
    private static readonly BulkStagingTable IdentitiesStaging = new(
        """
        CREATE TEMP TABLE staging_candidate_identities (
            "Id" uuid,
            "Cpf" varchar(11),
            "FullName" varchar(200),
            "BirthDate" date,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_candidate_identities
            ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // Candidate identities are immutable. Once a natural key exists its attributes are the
    // canonical record, so a later import must resolve to that row instead of overwriting it.
    // The previous DO UPDATE was only there to make the statement self-reporting (RETURNING
    // suppresses conflicting rows unless the row is updated); the read-back below removes that
    // requirement and turns a re-import into an insert-only no-op.
    //
    // Ordering the insert by the conflict key means two concurrent imports that share identities
    // acquire the unique-index locks in the same order, which removes the most likely deadlock
    // pattern between scopes that process different states of the same year. DISTINCT ON still
    // collapses a key repeated inside one batch, because PostgreSQL rejects a statement that
    // would affect one row twice; ctid ordering keeps the last staged row, matching the
    // accumulation order of the import source.
    private const string InsertIdentitiesWithCpfSql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        SELECT "Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("Cpf")
                   "Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt"
            FROM staging_candidate_identities
            WHERE "Cpf" IS NOT NULL
            ORDER BY "Cpf", "ctid" DESC
        ) AS staged
        ORDER BY "Cpf"
        ON CONFLICT ("Cpf") DO NOTHING;
        """;

    private const string InsertIdentitiesWithoutCpfSql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        SELECT "Id", NULL, "FullName", "BirthDate", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("FullName", "BirthDate")
                   "Id", "FullName", "BirthDate", "CreatedAt", "UpdatedAt"
            FROM staging_candidate_identities
            WHERE "Cpf" IS NULL
            ORDER BY "FullName", "BirthDate", "ctid" DESC
        ) AS staged
        ORDER BY "FullName", "BirthDate"
        ON CONFLICT ("FullName", "BirthDate") WHERE "Cpf" IS NULL DO NOTHING;
        """;

    // The read-back runs as its own statement, after the inserts, so under READ COMMITTED it
    // takes a fresh snapshot. That is what lets it observe a row committed by a competing import
    // that won the conflict: `INSERT ... ON CONFLICT DO NOTHING` waits for that transaction and
    // then skips, and the following SELECT sees the committed row. One statement covers both
    // partitions so the batch costs one roundtrip regardless of how many identities it contains.
    //
    // The join is on the natural key the unique indexes enforce, so it can never return a row for
    // a different person. `IS NOT DISTINCT FROM` is required because a candidate may have neither
    // CPF nor birth date and the partial index treats those NULLs as equal.
    private const string SelectPersistedIdentitiesSql = """
        SELECT identity."Id", identity."Cpf", identity."FullName", identity."BirthDate"
        FROM "CandidateIdentities" AS identity
        INNER JOIN (
            SELECT DISTINCT "Cpf"
            FROM staging_candidate_identities
            WHERE "Cpf" IS NOT NULL
        ) AS staged ON identity."Cpf" = staged."Cpf"
        UNION ALL
        SELECT identity."Id", identity."Cpf", identity."FullName", identity."BirthDate"
        FROM "CandidateIdentities" AS identity
        INNER JOIN (
            SELECT DISTINCT "FullName", "BirthDate"
            FROM staging_candidate_identities
            WHERE "Cpf" IS NULL
        ) AS staged
            ON identity."Cpf" IS NULL
           AND identity."FullName" = staged."FullName"
           AND identity."BirthDate" IS NOT DISTINCT FROM staged."BirthDate"
        ORDER BY 2, 3, 4;
        """;

    private static readonly BulkStagingTable CandidatesStaging = new(
        """
        CREATE TEMP TABLE staging_candidates (
            "Id" uuid,
            "ElectionId" uuid,
            "OfficeId" uuid,
            "StateId" uuid,
            "CandidateIdentityId" uuid,
            "TseCandidateSequential" varchar(50),
            "BallotNumber" integer,
            "Name" varchar(200),
            "UrnName" varchar(200),
            "SocialName" varchar(200),
            "PartyId" uuid,
            "FederationId" uuid,
            "CoalitionId" uuid,
            "RegistrationStatus" varchar(100),
            "Education" varchar(150),
            "Occupation" varchar(150),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_candidates
            ("Id", "ElectionId", "OfficeId", "StateId", "CandidateIdentityId",
             "TseCandidateSequential", "BallotNumber", "Name", "UrnName", "SocialName",
             "PartyId", "FederationId", "CoalitionId", "RegistrationStatus", "Education",
             "Occupation", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // DISTINCT ON keeps one row per conflict key: repeating a candidate inside one batch would
    // otherwise make PostgreSQL reject the statement with "ON CONFLICT DO UPDATE command cannot
    // affect row a second time". ctid ordering makes the last staged row win, matching the
    // accumulation order of the import source. The outer ORDER BY reproduces the conflict-key
    // ordering so concurrent batches lock candidate rows consistently.
    //
    // The WHERE clause turns a re-import of unchanged data into no write at all. Only mutable
    // business columns are compared: the conflict-key columns are equal by definition and
    // UpdatedAt changes on every construction, so comparing it would defeat the guard.
    private const string MergeCandidatesSql = """
        INSERT INTO "Candidates" AS target
            ("Id", "ElectionId", "OfficeId", "StateId", "CandidateIdentityId",
             "TseCandidateSequential", "BallotNumber", "Name", "UrnName", "SocialName",
             "PartyId", "FederationId", "CoalitionId", "RegistrationStatus", "Education",
             "Occupation", "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "ElectionId", "OfficeId", "StateId", "CandidateIdentityId",
            "TseCandidateSequential", "BallotNumber", "Name", "UrnName", "SocialName",
            "PartyId", "FederationId", "CoalitionId", "RegistrationStatus", "Education",
            "Occupation", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("ElectionId", "StateId", "OfficeId",
                                "TseCandidateSequential", "BallotNumber", "Name")
                   "Id", "ElectionId", "OfficeId", "StateId", "CandidateIdentityId",
                   "TseCandidateSequential", "BallotNumber", "Name", "UrnName", "SocialName",
                   "PartyId", "FederationId", "CoalitionId", "RegistrationStatus", "Education",
                   "Occupation", "CreatedAt", "UpdatedAt"
            FROM staging_candidates
            ORDER BY "ElectionId", "StateId", "OfficeId",
                     "TseCandidateSequential", "BallotNumber", "Name", "ctid" DESC
        ) AS staged
        ORDER BY "ElectionId", "StateId", "OfficeId",
                 "TseCandidateSequential", "BallotNumber", "Name"
        ON CONFLICT ("ElectionId", "StateId", "OfficeId", "TseCandidateSequential",
                     "BallotNumber", "Name") DO UPDATE SET
            "CandidateIdentityId" = EXCLUDED."CandidateIdentityId",
            "UrnName" = EXCLUDED."UrnName",
            "SocialName" = EXCLUDED."SocialName",
            "PartyId" = EXCLUDED."PartyId",
            "FederationId" = EXCLUDED."FederationId",
            "CoalitionId" = EXCLUDED."CoalitionId",
            "RegistrationStatus" = EXCLUDED."RegistrationStatus",
            "Education" = EXCLUDED."Education",
            "Occupation" = EXCLUDED."Occupation",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(
            target."CandidateIdentityId", target."UrnName", target."SocialName",
            target."PartyId", target."FederationId", target."CoalitionId",
            target."RegistrationStatus", target."Education", target."Occupation"
        ) IS DISTINCT FROM ROW(
            EXCLUDED."CandidateIdentityId", EXCLUDED."UrnName", EXCLUDED."SocialName",
            EXCLUDED."PartyId", EXCLUDED."FederationId", EXCLUDED."CoalitionId",
            EXCLUDED."RegistrationStatus", EXCLUDED."Education", EXCLUDED."Occupation"
        );
        """;

    private const string SelectCandidateIdsSql = """
        SELECT "TseCandidateSequential", "Id"
        FROM "Candidates"
        WHERE "ElectionId" = @ElectionId
          AND "StateId" = @StateId;
        """;

    public async Task<CandidateIdentityResolution> EnsureIdentitiesAsync(
        IReadOnlyCollection<CandidateIdentity> identities,
        CancellationToken cancellationToken = default)
    {
        if (identities.Count == 0)
            return CandidateIdentityResolution.Empty;

        // One transaction, one COPY and three statements: two insert-only partitions and one
        // bulk read-back. Nothing is ever updated, so re-importing the same people costs no row
        // versions, no WAL for updates and no index maintenance beyond the conflict check.
        var persisted = await BulkCopy.ExecuteAsync(
            dataSource,
            IdentitiesStaging,
            identities,
            WriteIdentityAsync,
            async (connection, transaction, token) =>
            {
                await using (var withCpf = new NpgsqlCommand(
                                 InsertIdentitiesWithCpfSql, connection, transaction))
                {
                    await withCpf.ExecuteNonQueryAsync(token);
                }

                await using (var withoutCpf = new NpgsqlCommand(
                                 InsertIdentitiesWithoutCpfSql, connection, transaction))
                {
                    await withoutCpf.ExecuteNonQueryAsync(token);
                }

                return await ReadPersistedIdentitiesAsync(connection, transaction, token);
            },
            cancellationToken);

        return Map(identities, persisted);
    }

    /// <summary>
    /// Resolves the incoming batch against the rows the database actually holds.
    /// </summary>
    /// <remarks>
    /// Every distinct input key must be present after the read-back. A missing key is not a
    /// recoverable condition: it would silently drop a candidate's identity link, so it fails
    /// loudly instead.
    ///
    /// A divergence is only reported when the winning row did not come from this batch. A newly
    /// inserted row always carries the identifier of one of the input entities, so an identifier
    /// that is not in the input proves the row already existed. That distinction keeps a duplicate
    /// key inside one batch (a source-quality problem) from being reported as an immutability
    /// conflict, which would be misleading.
    /// </remarks>
    private static CandidateIdentityResolution Map(
        IReadOnlyCollection<CandidateIdentity> identities,
        IReadOnlyList<IdentityRow> persisted)
    {
        var stored = new Dictionary<string, IdentityRow>(persisted.Count, StringComparer.Ordinal);
        var inserted = new HashSet<Guid>(identities.Count);

        foreach (var identity in identities)
            inserted.Add(identity.Id);

        foreach (var row in persisted)
            stored[CandidateIdentityKey.Of(row.Cpf, row.FullName, row.BirthDate)] = row;

        var ids = new Dictionary<string, Guid>(identities.Count, StringComparer.Ordinal);
        var conflicts = new Dictionary<Guid, CandidateIdentityConflict>();

        foreach (var identity in identities)
        {
            var key = CandidateIdentityResolution.KeyOf(identity);

            if (!stored.TryGetValue(key, out var row))
            {
                // The CPF is deliberately not part of the message.
                throw new InvalidOperationException(
                    $"Candidate identity '{identity.FullName}' was neither inserted nor found " +
                    "after the batch was persisted.");
            }

            ids[key] = row.Id;

            if (inserted.Contains(row.Id))
                continue;

            if (!row.ConflictsWith(identity.Cpf, identity.FullName, identity.BirthDate))
                continue;

            var fullNameDiffers = !string.Equals(
                row.FullName, identity.FullName, StringComparison.Ordinal);
            var birthDateDiffers = row.BirthDate != identity.BirthDate;

            // A batch can carry the same key with several attribute variants. Reporting one
            // diagnostic per stored identity keeps the signal at "this record disagreed with the
            // canonical row" instead of one warning per duplicate source row.
            conflicts[row.Id] = conflicts.TryGetValue(row.Id, out var existing)
                ? existing with
                {
                    FullNameDiffers = existing.FullNameDiffers || fullNameDiffers,
                    BirthDateDiffers = existing.BirthDateDiffers || birthDateDiffers
                }
                : new CandidateIdentityConflict(row.Id, fullNameDiffers, birthDateDiffers);
        }

        return new CandidateIdentityResolution(ids, [.. conflicts.Values]);
    }

    private static async Task<IReadOnlyList<IdentityRow>> ReadPersistedIdentitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            SelectPersistedIdentitiesSql, connection, transaction);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<IdentityRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new IdentityRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3)));
        }

        return rows;
    }

    private static async Task WriteIdentityAsync(
        NpgsqlBinaryImporter importer,
        CandidateIdentity identity,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(identity.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(identity.Cpf, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(identity.FullName, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(identity.BirthDate, NpgsqlDbType.Date, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(identity.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(identity.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private sealed record IdentityRow(Guid Id, string? Cpf, string FullName, DateOnly? BirthDate)
    {
        public bool ConflictsWith(string? cpf, string fullName, DateOnly? birthDate)
            => !string.Equals(Cpf, cpf, StringComparison.Ordinal)
               || !string.Equals(FullName, fullName, StringComparison.Ordinal)
               || BirthDate != birthDate;
    }

    public Task<int> UpsertCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
            return Task.FromResult(0);

        return BulkCopy.UpsertAsync(
            dataSource,
            CandidatesStaging,
            candidates,
            WriteCandidateAsync,
            MergeCandidatesSql,
            cancellationToken);
    }

    private static async Task WriteCandidateAsync(
        NpgsqlBinaryImporter importer,
        Candidate candidate,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(candidate.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(candidate.ElectionId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(candidate.OfficeId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(candidate.StateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(candidate.CandidateIdentityId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(candidate.TseCandidateSequential, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(candidate.BallotNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(candidate.Name, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(candidate.UrnName, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(candidate.SocialName, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(candidate.PartyId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(candidate.FederationId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(candidate.CoalitionId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(candidate.RegistrationStatus, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(candidate.Education, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(candidate.Occupation, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(candidate.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(candidate.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(string Sequential, Guid Id)>(new CommandDefinition(
            SelectCandidateIdsSql,
            new { ElectionId = electionId, StateId = stateId },
            cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => row.Sequential, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
    }
}
