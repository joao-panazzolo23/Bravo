using System.Text.Json;
using Bravo.Domain.Geography.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class StateImportBatchStore(NpgsqlDataSource dataSource)
    : DapperImportBatchStore<State>(dataSource)
{
    private const string UpsertSql = """
        INSERT INTO "States"
            ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
        SELECT
            record."Id", record."IbgeCode", record."Abbreviation",
            record."Name", record."Region", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid,
            "IbgeCode" integer,
            "Abbreviation" varchar(2),
            "Name" varchar(100),
            "Region" varchar(20),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("IbgeCode") DO UPDATE SET
            "Abbreviation" = EXCLUDED."Abbreviation",
            "Name" = EXCLUDED."Name",
            "Region" = EXCLUDED."Region",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    protected override async Task PersistRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<State> records,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(records.Select(state => new
        {
            state.Id,
            state.IbgeCode,
            state.Abbreviation,
            state.Name,
            state.Region,
            state.CreatedAt,
            state.UpdatedAt
        }));

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            transaction,
            cancellationToken: cancellationToken));
    }
}
