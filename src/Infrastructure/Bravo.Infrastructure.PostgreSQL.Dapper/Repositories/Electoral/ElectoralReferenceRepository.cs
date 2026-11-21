using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

/// <summary>
/// Reference upserts over binary COPY and a temporary staging table.
/// </summary>
/// <remarks>
/// This was the first repository migrated away from <c>jsonb_to_recordset</c> and is the pattern
/// the rest of the persistence layer now follows: one transaction, a temporary staging table
/// mirroring the destination types, binary COPY into it, then a single <c>INSERT ... SELECT</c>
/// merge with the original conflict target.
/// </remarks>
internal sealed class ElectoralReferenceRepository(NpgsqlDataSource dataSource)
    : IElectoralReferenceRepository
{
    private static readonly BulkStagingTable OfficesStaging = new(
        """
        CREATE TEMP TABLE staging_offices (
            "Id" uuid,
            "TseOfficeCode" integer,
            "Name" varchar(100),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_offices ("Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // DISTINCT ON keeps a single row per conflict target. Without it, a batch containing the
    // same key twice would make PostgreSQL reject the whole statement with "ON CONFLICT DO
    // UPDATE command cannot affect row a second time". ctid ordering means the last staged row
    // wins, matching the accumulation order used by the import source.
    //
    // The WHERE guard skips a write when the name did not change. Reference data is re-read for
    // every year, so most of these rows are identical on almost every run.
    private const string MergeOfficesSql = """
        INSERT INTO "ElectoralOffices" AS target
            ("Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt")
        SELECT "Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("TseOfficeCode")
                   "Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt"
            FROM staging_offices
            ORDER BY "TseOfficeCode", "ctid" DESC
        ) AS staged
        ORDER BY "TseOfficeCode"
        ON CONFLICT ("TseOfficeCode") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."Name" IS DISTINCT FROM EXCLUDED."Name";
        """;

    private static readonly BulkStagingTable PartiesStaging = new(
        """
        CREATE TEMP TABLE staging_parties (
            "Id" uuid,
            "TsePartyNumber" integer,
            "Acronym" text,
            "Name" varchar(150),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_parties ("Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    private const string MergePartiesSql = """
        INSERT INTO "PoliticalParties" AS target
            ("Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt")
        SELECT "Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("TsePartyNumber", "Acronym")
                   "Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt"
            FROM staging_parties
            ORDER BY "TsePartyNumber", "Acronym", "ctid" DESC
        ) AS staged
        ORDER BY "TsePartyNumber", "Acronym"
        ON CONFLICT ("TsePartyNumber", "Acronym") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."Name" IS DISTINCT FROM EXCLUDED."Name";
        """;

    private static readonly BulkStagingTable FederationsStaging = new(
        """
        CREATE TEMP TABLE staging_federations (
            "Id" uuid,
            "TseFederationNumber" integer,
            "Name" varchar(150),
            "Acronym" text,
            "Composition" text,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_federations
            ("Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    private const string MergeFederationsSql = """
        INSERT INTO "Federations" AS target
            ("Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt")
        SELECT "Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("TseFederationNumber")
                   "Id", "TseFederationNumber", "Name", "Acronym", "Composition",
                   "CreatedAt", "UpdatedAt"
            FROM staging_federations
            ORDER BY "TseFederationNumber", "ctid" DESC
        ) AS staged
        ORDER BY "TseFederationNumber"
        ON CONFLICT ("TseFederationNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Acronym" = EXCLUDED."Acronym",
            "Composition" = EXCLUDED."Composition",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(target."Name", target."Acronym", target."Composition")
              IS DISTINCT FROM ROW(EXCLUDED."Name", EXCLUDED."Acronym", EXCLUDED."Composition");
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

    public Task<int> UpsertOfficesAsync(
        IReadOnlyCollection<ElectoralOffice> offices,
        CancellationToken cancellationToken = default)
        => offices.Count == 0
            ? Task.FromResult(0)
            : BulkCopy.UpsertAsync(
                dataSource, OfficesStaging, offices, WriteOfficeAsync, MergeOfficesSql, cancellationToken);

    public Task<int> UpsertPartiesAsync(
        IReadOnlyCollection<PoliticalParty> parties,
        CancellationToken cancellationToken = default)
        => parties.Count == 0
            ? Task.FromResult(0)
            : BulkCopy.UpsertAsync(
                dataSource, PartiesStaging, parties, WritePartyAsync, MergePartiesSql, cancellationToken);

    public Task<int> UpsertFederationsAsync(
        IReadOnlyCollection<Federation> federations,
        CancellationToken cancellationToken = default)
        => federations.Count == 0
            ? Task.FromResult(0)
            : BulkCopy.UpsertAsync(
                dataSource, FederationsStaging, federations, WriteFederationAsync,
                MergeFederationsSql, cancellationToken);

    private static async Task WriteOfficeAsync(
        NpgsqlBinaryImporter importer,
        ElectoralOffice office,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(office.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(office.TseOfficeCode, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(office.Name, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(office.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(office.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
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
        await importer.WriteAsync(BulkCopy.AsUtc(party.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(party.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
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
        await importer.WriteNullableAsync(federation.Composition, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(federation.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(federation.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

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
