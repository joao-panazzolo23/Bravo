using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectionRepository(NpgsqlDataSource dataSource) : IElectionRepository
{
    // Elections and rounds are reference data keyed by a stable TSE code, so the import re-reads
    // the same rows on every run. The guard turns a re-import of unchanged data into no physical
    // write at all.
    //
    // Only mutable business columns are compared. The conflict-key columns are equal by
    // definition and "UpdatedAt" changes on every construction, so including them would defeat
    // the guard.
    //
    // A guard also suppresses RETURNING for the unchanged rows (PostgreSQL only returns rows the
    // statement actually wrote). The caller still needs the stored identifier, so the repository
    // falls back to an indexed SELECT by the natural key. That keeps the common paths at one
    // statement: an insert returns the new row, and a genuine change returns the updated row.
    private const string UpsertElectionSql = """
        INSERT INTO "Elections" AS target
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
        WHERE ROW(
            target."Year", target."TypeCode", target."TypeName",
            target."Description", target."ElectionDate"
        ) IS DISTINCT FROM ROW(
            EXCLUDED."Year", EXCLUDED."TypeCode", EXCLUDED."TypeName",
            EXCLUDED."Description", EXCLUDED."ElectionDate"
        )
        RETURNING *;
        """;

    private const string SelectElectionByCodeSql = """
        SELECT * FROM "Elections" WHERE "TseElectionCode" = @TseElectionCode;
        """;

    private const string UpsertRoundSql = """
        INSERT INTO "ElectionRounds" AS target
            ("Id", "ElectionId", "Round", "IsRunoff", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @ElectionId, @Round, @IsRunoff, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("ElectionId", "Round") DO UPDATE SET
            "IsRunoff" = EXCLUDED."IsRunoff",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."IsRunoff" IS DISTINCT FROM EXCLUDED."IsRunoff"
        RETURNING *;
        """;

    private const string SelectRoundSql = """
        SELECT * FROM "ElectionRounds"
        WHERE "ElectionId" = @ElectionId AND "Round" = @Round;
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

        var parameters = new
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
        };

        var written = await connection.QuerySingleOrDefaultAsync<Election>(new CommandDefinition(
            UpsertElectionSql,
            parameters,
            cancellationToken: cancellationToken));

        // A null result means the conflict branch was skipped because nothing changed, so the
        // canonical row is read by its unique code instead of being rewritten.
        return written ?? await connection.QuerySingleAsync<Election>(new CommandDefinition(
            SelectElectionByCodeSql,
            new { election.TseElectionCode },
            cancellationToken: cancellationToken));
    }

    public async Task<ElectionRound> UpsertRoundAsync(
        ElectionRound round,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var parameters = new
        {
            round.Id,
            round.ElectionId,
            round.Round,
            round.IsRunoff,
            round.CreatedAt,
            round.UpdatedAt
        };

        var written = await connection.QuerySingleOrDefaultAsync<ElectionRound>(new CommandDefinition(
            UpsertRoundSql,
            parameters,
            cancellationToken: cancellationToken));

        return written ?? await connection.QuerySingleAsync<ElectionRound>(new CommandDefinition(
            SelectRoundSql,
            new { round.ElectionId, round.Round },
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
