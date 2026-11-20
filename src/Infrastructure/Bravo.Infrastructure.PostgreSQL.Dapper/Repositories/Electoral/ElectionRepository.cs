using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectionRepository(NpgsqlDataSource dataSource) : IElectionRepository
{
    private const string UpsertElectionSql = """
        INSERT INTO "Elections"
            ("Id", "TseElectionCode", "Year", "TypeCode", "TypeName", "Description",
             "ElectionDate", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @TseElectionCode, @Year, @TypeCode, @TypeName, @Description,
             @ElectionDate, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("TseElectionCode") DO UPDATE SET
            "Year" = EXCLUDED."Year",
            "TypeCode" = EXCLUDED."TypeCode",
            "TypeName" = EXCLUDED."TypeName",
            "Description" = EXCLUDED."Description",
            "ElectionDate" = EXCLUDED."ElectionDate",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING *;
        """;

    private const string UpsertRoundSql = """
        INSERT INTO "ElectionRounds"
            ("Id", "ElectionId", "Round", "Name", "IsRunoff", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @ElectionId, @Round, @Name, @IsRunoff, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("ElectionId", "Round") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "IsRunoff" = EXCLUDED."IsRunoff",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING *;
        """;

    private const string ListElectionsSql = """
        SELECT * FROM "Elections";
        """;

    private const string ListRoundsSql = """
        SELECT * FROM "ElectionRounds";
        """;

    public async Task<Election> UpsertAsync(Election election, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleAsync<Election>(new CommandDefinition(
            UpsertElectionSql,
            new
            {
                election.Id,
                election.TseElectionCode,
                election.Year,
                election.TypeCode,
                election.TypeName,
                election.Description,
                election.ElectionDate,
                election.CreatedAt,
                election.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<ElectionRound> UpsertRoundAsync(
        ElectionRound round,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleAsync<ElectionRound>(new CommandDefinition(
            UpsertRoundSql,
            new
            {
                round.Id,
                round.ElectionId,
                round.Round,
                round.Name,
                round.IsRunoff,
                round.CreatedAt,
                round.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Election>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<Election>(new CommandDefinition(
            ListElectionsSql,
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<ElectionRound>> ListRoundsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ElectionRound>(new CommandDefinition(
            ListRoundsSql,
            cancellationToken: cancellationToken));

        return rows.AsList();
    }
}

