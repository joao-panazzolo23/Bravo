using System.Text.Json;
using Bravo.Application.Electoral.Contracts;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class CoalitionRepository(NpgsqlDataSource dataSource) : ICoalitionRepository
{
    private const string UpsertSql = """
        INSERT INTO "Coalitions"
            ("Id", "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber",
             "Name", "Composition", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."ElectionRoundId", record."OfficeId", record."StateId",
               record."TseCoalitionNumber", record."Name", record."Composition",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "ElectionRoundId" uuid, "OfficeId" uuid, "StateId" uuid,
            "TseCoalitionNumber" bigint, "Name" varchar(150), "Composition" text,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Composition" = EXCLUDED."Composition",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectSql = """
        SELECT "ElectionRoundId", "OfficeId", "TseCoalitionNumber", "Id" FROM "Coalitions";
        """;

    public async Task<int> UpsertAsync(
        IReadOnlyCollection<Coalition> coalitions,
        CancellationToken cancellationToken = default)
    {
        if (coalitions.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(coalitions.Select(coalition => new
        {
            coalition.Id,
            coalition.ElectionRoundId,
            coalition.OfficeId,
            coalition.StateId,
            coalition.TseCoalitionNumber,
            coalition.Name,
            coalition.Composition,
            coalition.CreatedAt,
            coalition.UpdatedAt
        }));

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<(Guid RoundId, Guid OfficeId, long Number), Guid>> GetIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<CoalitionKeyRow>(new CommandDefinition(
            SelectSql,
            cancellationToken: cancellationToken));

        return rows.ToDictionary(
            row => (row.ElectionRoundId, row.OfficeId, row.TseCoalitionNumber),
            row => row.Id);
    }

    private sealed record CoalitionKeyRow(
        Guid ElectionRoundId,
        Guid OfficeId,
        long TseCoalitionNumber,
        Guid Id);
}
