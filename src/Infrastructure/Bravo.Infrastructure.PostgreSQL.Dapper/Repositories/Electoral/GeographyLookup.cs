using Bravo.Domain.Electoral.Services;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class GeographyLookup(NpgsqlDataSource dataSource) : IGeographyRepository
{
    // States and municipalities used to be serialized to JSON and parsed back by
    // jsonb_to_recordset. They now stream into a temporary table with binary COPY and merge with
    // one INSERT ... SELECT.
    private static readonly BulkStagingTable StatesStaging = new(
        """
        CREATE TEMP TABLE staging_states (
            "Id" uuid,
            "IbgeCode" integer,
            "Abbreviation" varchar(20),
            "Name" varchar(100),
            "Region" varchar(20),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_states
            ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // Abbreviation is the conflict key and is immutable, so only Name and Region are compared.
    // The IBGE import writes the same 27 rows on every run; without the guard each run rewrote
    // every state row for no observable change.
    private const string MergeStatesSql = """
        INSERT INTO "States" AS target
            ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
        SELECT "Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("Abbreviation")
                   "Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt"
            FROM staging_states
            ORDER BY "Abbreviation", "ctid" DESC
        ) AS staged
        ORDER BY "Abbreviation"
        ON CONFLICT ("Abbreviation") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Region" = EXCLUDED."Region",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(target."Name", target."Region")
              IS DISTINCT FROM ROW(EXCLUDED."Name", EXCLUDED."Region");
        """;

    private static readonly BulkStagingTable MunicipalitiesStaging = new(
        """
        CREATE TEMP TABLE staging_municipalities (
            "Id" uuid,
            "IbgeCode" integer,
            "Name" varchar(150),
            "StateIbgeCode" integer,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_municipalities
            ("Id", "IbgeCode", "Name", "StateIbgeCode", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // The state identifier is resolved by joining on the IBGE code, exactly as the original
    // statement did, so a municipality whose state is not imported yet is still skipped instead
    // of being stored with a bogus reference.
    private const string MergeMunicipalitiesSql = """
        INSERT INTO "Municipalities" AS target
            ("Id", "IbgeCode", "Name", "StateId", "CreatedAt", "UpdatedAt")
        SELECT staged."Id", staged."IbgeCode", staged."Name",
               state."Id", staged."CreatedAt", staged."UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("IbgeCode")
                   "Id", "IbgeCode", "Name", "StateIbgeCode", "CreatedAt", "UpdatedAt"
            FROM staging_municipalities
            ORDER BY "IbgeCode", "ctid" DESC
        ) AS staged
        JOIN "States" AS state ON state."IbgeCode" = staged."StateIbgeCode"
        ORDER BY staged."IbgeCode"
        ON CONFLICT ("IbgeCode") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "StateId" = EXCLUDED."StateId",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(target."Name", target."StateId")
              IS DISTINCT FROM ROW(EXCLUDED."Name", EXCLUDED."StateId");
        """;

    private const string SelectStatesSql = """
        SELECT "Abbreviation", "Id" FROM "States";
        """;

    private const string SelectMunicipalitiesSql = """
        SELECT "IbgeCode", "Id" FROM "Municipalities";
        """;

    private const string SelectMunicipalitiesFullSql = """
        SELECT "Id", "StateId", "Name", "IbgeCode" FROM "Municipalities";
        """;

    private const string SelectMunicipalitiesByStateSql = """
        SELECT "Id", "StateId", "Name", "IbgeCode"
        FROM "Municipalities"
        WHERE "StateId" = @StateId;
        """;

    public async Task<IReadOnlyDictionary<string, Guid>> GetStateIdsByAcronymAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<StateRow>(new CommandDefinition(
            SelectStatesSql,
            cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.Abbreviation, row => row.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetMunicipalityIdsByIbgeCodeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(int IbgeCode, Guid Id)>(new CommandDefinition(
            SelectMunicipalitiesSql,
            cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.IbgeCode, row => row.Id);
    }

    public async Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<MunicipalityLookup>(new CommandDefinition(
            SelectMunicipalitiesFullSql,
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<MunicipalityLookup>> GetMunicipalitiesByStateAsync(
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<MunicipalityLookup>(new CommandDefinition(
            SelectMunicipalitiesByStateSql,
            new { StateId = stateId },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    private sealed record StateRow(string Abbreviation, Guid Id);

    public Task<int> UpsertMunicipalitiesAsync(
        IReadOnlyCollection<MunicipalityUpsert> municipalities,
        CancellationToken cancellationToken = default)
    {
        if (municipalities.Count == 0)
            return Task.FromResult(0);

        // The source does not carry identifiers, so the batch is projected to fresh version-7
        // identifiers and one timestamp before being copied.
        var now = DateTime.UtcNow;

        var rows = municipalities
            .Select(municipality => new MunicipalityRow(
                Guid.CreateVersion7(),
                municipality.IbgeCode,
                municipality.Name,
                municipality.StateIbgeCode,
                now))
            .ToArray();

        // The caller treats a return value below the input count as "some states are missing"
        // and aborts the import. The count is therefore taken from the staged rows that joined a
        // state, not from affected rows: the no-op guard skips the physical write but the row is
        // still considered persisted, which keeps the caller's assertion and progress counters
        // exactly as they were before the migration.
        return BulkCopy.ExecuteAsync(
            dataSource,
            MunicipalitiesStaging,
            rows,
            WriteMunicipalityAsync,
            async (connection, transaction, token) =>
            {
                await using (var merge = new NpgsqlCommand(MergeMunicipalitiesSql, connection, transaction))
                {
                    await merge.ExecuteNonQueryAsync(token);
                }

                await using var matched = new NpgsqlCommand(CountMatchedMunicipalitiesSql, connection, transaction);

                return (int)(long)(await matched.ExecuteScalarAsync(token))!;
            },
            cancellationToken);
    }

    private const string CountMatchedMunicipalitiesSql = """
        SELECT COUNT(*)
        FROM staging_municipalities AS staged
        JOIN "States" AS state ON state."IbgeCode" = staged."StateIbgeCode";
        """;

    private static async Task WriteMunicipalityAsync(
        NpgsqlBinaryImporter importer,
        MunicipalityRow row,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(row.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(row.IbgeCode, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(row.Name, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(row.StateIbgeCode, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(row.Timestamp, NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(row.Timestamp, NpgsqlDbType.TimestampTz, cancellationToken);
    }

    public Task<int> UpsertStatesAsync(
        IReadOnlyCollection<State> states,
        CancellationToken cancellationToken = default)
    {
        if (states.Count == 0)
            return Task.FromResult(0);

        return BulkCopy.UpsertAsync(
            dataSource,
            StatesStaging,
            states,
            WriteStateAsync,
            MergeStatesSql,
            cancellationToken);
    }

    private static async Task WriteStateAsync(
        NpgsqlBinaryImporter importer,
        State state,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(state.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(state.IbgeCode, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(state.Abbreviation, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(state.Name, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(state.Region, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(state.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(state.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    /// <summary>
    /// One municipality row as staged. It carries the source state code instead of the resolved
    /// identifier because the join happens inside the merge.
    /// </summary>
    private sealed record MunicipalityRow(
        Guid Id,
        int IbgeCode,
        string Name,
        int StateIbgeCode,
        DateTime Timestamp);
}
