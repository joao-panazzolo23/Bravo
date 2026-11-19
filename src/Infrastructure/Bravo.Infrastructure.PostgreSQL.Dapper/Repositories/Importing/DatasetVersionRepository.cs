using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Dapper;
using Npgsql;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class DatasetVersionRepository(NpgsqlDataSource dataSource) : IDatasetVersionRepository
{
    private const string InsertSql = """
        INSERT INTO "DatasetVersions"
            ("Id", "ImportDatasetId", "Scope", "Status", "IsActive", "ImportJobId",
             "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @ImportDatasetId, @Scope, @Status, false, @ImportJobId,
             @CreatedAt, @UpdatedAt)
        ON CONFLICT ("ImportDatasetId", "Scope") DO UPDATE SET
            "Status" = EXCLUDED."Status",
            "ImportJobId" = EXCLUDED."ImportJobId",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING *;
        """;

    private const string SetStatusSql = """
        UPDATE "DatasetVersions"
        SET "Status" = @Status,
            "UpdatedAt" = now()
        WHERE "Id" = @Id;
        """;

    private const string SelectActiveSql = """
        SELECT *
        FROM "DatasetVersions"
        WHERE "Scope" = @Scope
          AND "IsActive";
        """;

    private const string SupersedePreviousSql = """
        UPDATE "DatasetVersions"
        SET "IsActive" = false,
            "Status" = 'Superseded',
            "SupersededAt" = now(),
            "UpdatedAt" = now()
        WHERE "Scope" = (SELECT "Scope" FROM "DatasetVersions" WHERE "Id" = @Id)
          AND "IsActive"
          AND "Id" <> @Id;
        """;

    private const string ActivateSql = """
        UPDATE "DatasetVersions"
        SET "IsActive" = true,
            "Status" = 'Completed',
            "ActivatedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id;
        """;

    public async Task<DatasetVersion> CreateAsync(
        DatasetVersion version,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleAsync<DatasetVersion>(new CommandDefinition(
            InsertSql,
            new
            {
                version.Id,
                version.ImportDatasetId,
                version.Scope,
                Status = version.Status.ToString(),
                version.ImportJobId,
                version.CreatedAt,
                version.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SetStatusAsync(
        Guid id,
        DatasetVersionStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            SetStatusSql,
            new { Id = id, Status = status.ToString() },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<DatasetVersion?> GetActiveAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<DatasetVersion>(new CommandDefinition(
            SelectActiveSql,
            new { Scope = scope },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            SupersedePreviousSql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            ActivateSql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return affected > 0;
    }
}
