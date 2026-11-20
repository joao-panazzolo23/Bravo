using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectoralReferenceRepository(NpgsqlDataSource dataSource)
    : IElectoralReferenceRepository
{
    // Reference upserts stage rows in a temporary table with binary COPY, then merge with a
    // single INSERT ... SELECT. Compared with the previous jsonb_to_recordset approach this
    // avoids serializing every row to JSON, allocating the JSON string, and making PostgreSQL
    // parse it back into records.
    //
    // Staging columns use the same types as their destinations, so an oversized value fails
    // during COPY with the real database error (22001) instead of being truncated.
    private const string CreateStagingOfficesSql = """
        CREATE TEMP TABLE staging_offices (
            "Id" uuid,
            "TseOfficeCode" integer,
            "Name" varchar(100),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """;

    private const string CopyOfficesSql = """
        COPY staging_offices ("Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """;

    // DISTINCT ON keeps a single row per conflict target. Without it, a batch containing the
    // same key twice would make PostgreSQL reject the whole statement with "ON CONFLICT DO
    // UPDATE command cannot affect row a second time". ctid ordering means the last staged row
    // wins, matching the accumulation order used by the import source.
    private const string MergeOfficesSql = """
        INSERT INTO "ElectoralOffices" ("Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt")
        SELECT DISTINCT ON ("TseOfficeCode")
               "Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt"
        FROM staging_offices
        ORDER BY "TseOfficeCode", "ctid" DESC
        ON CONFLICT ("TseOfficeCode") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string CreateStagingPartiesSql = """
        CREATE TEMP TABLE staging_parties (
            "Id" uuid,
            "TsePartyNumber" integer,
            "Acronym" text,
            "Name" varchar(150),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """;

    private const string CopyPartiesSql = """
        COPY staging_parties ("Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """;

    private const string MergePartiesSql = """
        INSERT INTO "PoliticalParties"
            ("Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt")
        SELECT DISTINCT ON ("TsePartyNumber", "Acronym")
               "Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt"
        FROM staging_parties
        ORDER BY "TsePartyNumber", "Acronym", "ctid" DESC
        ON CONFLICT ("TsePartyNumber", "Acronym") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string CreateStagingFederationsSql = """
        CREATE TEMP TABLE staging_federations (
            "Id" uuid,
            "TseFederationNumber" integer,
            "Name" varchar(150),
            "Acronym" text,
            "Composition" text,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """;

    private const string CopyFederationsSql = """
        COPY staging_federations
            ("Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """;

    private const string MergeFederationsSql = """
        INSERT INTO "Federations"
            ("Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt")
        SELECT DISTINCT ON ("TseFederationNumber")
               "Id", "TseFederationNumber", "Name", "Acronym", "Composition",
               "CreatedAt", "UpdatedAt"
        FROM staging_federations
        ORDER BY "TseFederationNumber", "ctid" DESC
        ON CONFLICT ("TseFederationNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Acronym" = EXCLUDED."Acronym",
            "Composition" = EXCLUDED."Composition",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectOfficesSql = """
        SELECT "TseOfficeCode", "Id" FROM "ElectoralOffices";
        """;

    private const string SelectPartiesSql = """
        SELECT "TsePartyNumber", "Acronym", "Id" FROM "PoliticalParties";
        """;

    private const string SelectFederationsSql = """
        SELECT "TseFederationNumber", "Id" FROM "Federations";
        """;

    public async Task<int> UpsertOfficesAsync(
        IReadOnlyCollection<ElectoralOffice> offices,
        CancellationToken cancellationToken = default)
    {
        if (offices.Count == 0)
            return 0;

        return await BulkUpsertAsync(
            offices,
            CreateStagingOfficesSql,
            CopyOfficesSql,
            MergeOfficesSql,
            WriteOfficeAsync,
            cancellationToken);
    }

    public async Task<int> UpsertPartiesAsync(
        IReadOnlyCollection<PoliticalParty> parties,
        CancellationToken cancellationToken = default)
    {
        if (parties.Count == 0)
            return 0;

        return await BulkUpsertAsync(
            parties,
            CreateStagingPartiesSql,
            CopyPartiesSql,
            MergePartiesSql,
            WritePartyAsync,
            cancellationToken);
    }

    public async Task<int> UpsertFederationsAsync(
        IReadOnlyCollection<Federation> federations,
        CancellationToken cancellationToken = default)
    {
        if (federations.Count == 0)
            return 0;

        return await BulkUpsertAsync(
            federations,
            CreateStagingFederationsSql,
            CopyFederationsSql,
            MergeFederationsSql,
            WriteFederationAsync,
            cancellationToken);
    }

    /// <summary>
    /// Creates a temp staging table, streams rows into it with binary COPY, then merges into the
    /// destination. Everything runs in one transaction, so a COPY or merge failure leaves the
    /// destination untouched and drops the staging table.
    /// </summary>
    private async Task<int> BulkUpsertAsync<T>(
        IReadOnlyCollection<T> records,
        string createStagingSql,
        string copySql,
        string mergeSql,
        Func<NpgsqlBinaryImporter, T, CancellationToken, Task> writeRow,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var create = new NpgsqlCommand(createStagingSql, connection, transaction))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var importer = await connection.BeginBinaryImportAsync(copySql, cancellationToken))
        {
            foreach (var record in records)
            {
                await importer.StartRowAsync(cancellationToken);
                await writeRow(importer, record, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        int affected;

        await using (var merge = new NpgsqlCommand(mergeSql, connection, transaction))
        {
            affected = await merge.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return affected;
    }

    private static async Task WriteOfficeAsync(
        NpgsqlBinaryImporter importer,
        ElectoralOffice office,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(office.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(office.TseOfficeCode, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(office.Name, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(AsUtc(office.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(AsUtc(office.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private static async Task WritePartyAsync(
        NpgsqlBinaryImporter importer,
        PoliticalParty party,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(party.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(party.TsePartyNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(party.Acronym, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(party.Name, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(AsUtc(party.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(AsUtc(party.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private static async Task WriteFederationAsync(
        NpgsqlBinaryImporter importer,
        Federation federation,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(federation.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(federation.TseFederationNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(federation.Name, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(federation.Acronym, NpgsqlDbType.Text, cancellationToken);

        if (federation.Composition is null)
            await importer.WriteNullAsync(cancellationToken);
        else
            await importer.WriteAsync(federation.Composition, NpgsqlDbType.Text, cancellationToken);

        await importer.WriteAsync(AsUtc(federation.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(AsUtc(federation.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    /// <summary>
    /// Npgsql requires UTC for <c>timestamptz</c>. Entities are created from
    /// <see cref="DateTime.UtcNow"/>, so an unspecified kind is already UTC in practice.
    /// </summary>
    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task<IReadOnlyDictionary<int, Guid>> GetOfficeIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(int Code, Guid Id)>(new CommandDefinition(
            SelectOfficesSql,
            cancellationToken: cancellationToken));

        return rows.GroupBy(row => row.Code).ToDictionary(g => g.Key, g => g.First().Id);
    }

    public async Task<IReadOnlyDictionary<(int Number, string Acronym), Guid>> GetPartyIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PartyKeyRow>(new CommandDefinition(
            SelectPartiesSql,
            cancellationToken: cancellationToken));

        // Acronym casing drifts between years (for example "PC do B" vs "PC DO B"), so the
        // key is case-insensitive and duplicate variants collapse to the first row.
        return rows
            .GroupBy(row => (row.TsePartyNumber, row.Acronym.ToUpperInvariant()))
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetFederationIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(int Number, Guid Id)>(new CommandDefinition(
            SelectFederationsSql,
            cancellationToken: cancellationToken));

        return rows.GroupBy(row => row.Number).ToDictionary(g => g.Key, g => g.First().Id);
    }

    private sealed record PartyKeyRow(int TsePartyNumber, string Acronym, Guid Id);
}
