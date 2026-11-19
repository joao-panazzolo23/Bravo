using Bravo.Domain.Electoral.Services;
using Dapper;
using Npgsql;
using Bravo.Domain.Geography.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class GeographyLookup(NpgsqlDataSource dataSource) : IGeographyRepository
{
    private const string SelectStatesSql = """
        SELECT "Abbreviation", "Id" FROM "States";
        """;

    private const string SelectMunicipalitiesSql = """
        SELECT "IbgeCode", "Id" FROM "Municipalities";
        """;

    private const string SelectMunicipalitiesFullSql = """
        SELECT "Id", "StateId", "Name", "IbgeCode" FROM "Municipalities";
        """;

    private const string UpsertStatesSql = """
        INSERT INTO "States" ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."IbgeCode", record."Abbreviation", record."Name",
               record."Region", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "IbgeCode" integer, "Abbreviation" varchar(20), "Name" varchar(100),
            "Region" varchar(20),
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("Abbreviation") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Region" = EXCLUDED."Region",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
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

    private sealed record StateRow(string Abbreviation, Guid Id);

    public async Task<int> UpsertStatesAsync(
        IReadOnlyCollection<Bravo.Domain.Geography.Entities.State> states,
        CancellationToken cancellationToken = default)
    {
        if (states.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = System.Text.Json.JsonSerializer.Serialize(states.Select(state => new
        {
            state.Id,
            state.IbgeCode,
            state.Abbreviation,
            state.Name,
            state.Region,
            state.CreatedAt,
            state.UpdatedAt
        }));

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertStatesSql,
            new { Records = payload },
            cancellationToken: cancellationToken));
    }
}
