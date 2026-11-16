using Bravo.Application.Electoral.Contracts;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class GeographyLookup(NpgsqlDataSource dataSource) : IGeographyLookup
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
}
