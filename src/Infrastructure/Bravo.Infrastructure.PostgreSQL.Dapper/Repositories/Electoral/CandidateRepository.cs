using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class CandidateRepository(NpgsqlDataSource dataSource) : ICandidateRepository
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

    // Ordering the insert by CPF means two concurrent imports that share identities acquire the
    // unique-index locks in the same order, which removes the most likely deadlock pattern
    // between work units that process different states in the same year.
    private const string MergeIdentitiesWithCpfSql = """
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
        -- Must update even when nothing changes: ON CONFLICT ... DO NOTHING would suppress the
        -- RETURNING row for an existing CPF, and the caller needs that id back. Keeping the
        -- no-op update is what makes the statement both idempotent and self-reporting, so this
        -- is the one upsert that deliberately has no no-op guard.
        ON CONFLICT ("Cpf") DO UPDATE SET
            "FullName" = EXCLUDED."FullName",
            "BirthDate" = EXCLUDED."BirthDate",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING "Id", "Cpf", "FullName", "BirthDate";
        """;

    private const string MergeIdentitiesWithoutCpfSql = """
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
        ON CONFLICT ("FullName", "BirthDate") WHERE "Cpf" IS NULL DO UPDATE SET
            "FullName" = EXCLUDED."FullName",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING "Id", "Cpf", "FullName", "BirthDate";
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

    public async Task<IReadOnlyDictionary<string, Guid>> UpsertIdentitiesAsync(
        IReadOnlyCollection<CandidateIdentity> identities,
        CancellationToken cancellationToken = default)
    {
        if (identities.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.Ordinal);

        // Both partitions share one COPY and one transaction. Splitting them keeps the partial
        // unique index usable: CPF rows conflict on Cpf, CPF-less rows on name and birth date.
        var rows = await BulkCopy.ExecuteAsync(
            dataSource,
            IdentitiesStaging,
            identities,
            WriteIdentityAsync,
            async (connection, transaction, token) =>
            {
                var persisted = new List<IdentityRow>(identities.Count);

                persisted.AddRange(await MergeIdentitiesAsync(
                    connection, transaction, MergeIdentitiesWithCpfSql, token));

                persisted.AddRange(await MergeIdentitiesAsync(
                    connection, transaction, MergeIdentitiesWithoutCpfSql, token));

                return (IReadOnlyList<IdentityRow>)persisted;
            },
            cancellationToken);

        var result = new Dictionary<string, Guid>(identities.Count, StringComparer.Ordinal);

        foreach (var row in rows)
            result[CandidateIdentityKey.Of(row.Cpf, row.FullName, row.BirthDate)] = row.Id;

        return result;
    }

    private static async Task<IReadOnlyList<IdentityRow>> MergeIdentitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);

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

    private sealed record IdentityRow(Guid Id, string? Cpf, string FullName, DateOnly? BirthDate);

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
