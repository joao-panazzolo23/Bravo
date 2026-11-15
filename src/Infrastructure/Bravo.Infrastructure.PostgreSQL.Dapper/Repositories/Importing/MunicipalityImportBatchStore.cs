using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class MunicipalityImportBatchStore(NpgsqlDataSource dataSource)
    : DapperImportBatchStore<IbgeMunicipalityRecord>(dataSource)
{
    private const string UpsertSql = """
        INSERT INTO "Municipalities"
            ("Id", "IbgeCode", "Name", "StateId", "CreatedAt", "UpdatedAt")
        SELECT
            record."Id", record."IbgeCode", record."Name",
            state."Id", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid,
            "IbgeCode" integer,
            "Name" varchar(150),
            "StateIbgeCode" integer,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone)
        JOIN "States" AS state ON state."IbgeCode" = record."StateIbgeCode"
        ON CONFLICT ("IbgeCode") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "StateId" = EXCLUDED."StateId",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    protected override async Task PersistRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<IbgeMunicipalityRecord> records,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var payload = JsonSerializer.Serialize(records.Select(record => new
        {
            Id = Guid.CreateVersion7(),
            IbgeCode = record.Code,
            record.Name,
            StateIbgeCode = record.StateCode,
            CreatedAt = now,
            UpdatedAt = now
        }));

        var persisted = await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            transaction,
            cancellationToken: cancellationToken));

        if (persisted != records.Count)
        {
            throw new InvalidOperationException(
                $"Persisted {persisted} municipalities but received {records.Count}; " +
                "some states are missing and must be imported first.");
        }
    }
}
