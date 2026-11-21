using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class CoalitionRepository(NpgsqlDataSource dataSource) : ICoalitionRepository
{
    // Coalitions used to be serialized to JSON and parsed back by jsonb_to_recordset. They now
    // stream into a temporary table with binary COPY and merge with one INSERT ... SELECT.
    private static readonly BulkStagingTable Staging = new(
        """
        CREATE TEMP TABLE staging_coalitions (
            "Id" uuid,
            "ElectionRoundId" uuid,
            "OfficeId" uuid,
            "StateId" uuid,
            "TseCoalitionNumber" bigint,
            "Name" varchar(150),
            "Composition" text,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_coalitions
            ("Id", "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber",
             "Name", "Composition", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    private const string MergeSql = """
        INSERT INTO "Coalitions" AS target
            ("Id", "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber",
             "Name", "Composition", "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber",
            "Name", "Composition", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber")
                   "Id", "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber",
                   "Name", "Composition", "CreatedAt", "UpdatedAt"
            FROM staging_coalitions
            ORDER BY "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber", "ctid" DESC
        ) AS staged
        ORDER BY "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber"
        ON CONFLICT ("ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Composition" = EXCLUDED."Composition",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(target."Name", target."Composition")
              IS DISTINCT FROM ROW(EXCLUDED."Name", EXCLUDED."Composition");
        """;

    private const string SelectSql = """
        SELECT "ElectionRoundId", "OfficeId", "TseCoalitionNumber", "Id"
        FROM "Coalitions"
        WHERE "StateId" = @StateId;
        """;

    public Task<int> UpsertAsync(
        IReadOnlyCollection<Coalition> coalitions,
        CancellationToken cancellationToken = default)
    {
        if (coalitions.Count == 0)
            return Task.FromResult(0);

        return BulkCopy.UpsertAsync(
            dataSource,
            Staging,
            coalitions,
            WriteCoalitionAsync,
            MergeSql,
            cancellationToken);
    }

    private static async Task WriteCoalitionAsync(
        NpgsqlBinaryImporter importer,
        Coalition coalition,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(coalition.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(coalition.ElectionRoundId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(coalition.OfficeId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(coalition.StateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(coalition.TseCoalitionNumber, NpgsqlDbType.Bigint, cancellationToken);
        await importer.WriteAsync(coalition.Name, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(coalition.Composition, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(coalition.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(coalition.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid RoundId, Guid OfficeId, long Number), Guid>> GetIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CoalitionKeyRow>(new CommandDefinition(
            SelectSql,
            new { StateId = stateId },
            cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => (row.ElectionRoundId, row.OfficeId, row.TseCoalitionNumber))
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    private sealed record CoalitionKeyRow(
        Guid ElectionRoundId,
        Guid OfficeId,
        long TseCoalitionNumber,
        Guid Id
    );
}
