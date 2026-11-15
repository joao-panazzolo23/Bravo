using Bravo.Application.Importing.Contracts;
using Bravo.Domain.Importing.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class ImportJobRepository(NpgsqlDataSource dataSource) : IImportJobRepository
{
    private const string InsertJobSql = """
        INSERT INTO "ImportJobs"
            ("Id", "ImportType", "Status", "ConflictStrategy", "BatchSize", "MaxAttempts",
             "LeaseDurationSeconds", "TotalProcessedRecords", "TotalFailedRecords", "LastError",
             "CreatedAt", "StartedAt", "CompletedAt", "UpdatedAt")
        VALUES
            (@Id, @ImportType, @Status, @ConflictStrategy, @BatchSize, @MaxAttempts,
             @LeaseDurationSeconds, @TotalProcessedRecords, @TotalFailedRecords, @LastError,
             @CreatedAt, @StartedAt, @CompletedAt, @UpdatedAt);
        """;

    private const string InsertWorkUnitSql = """
        INSERT INTO "ImportWorkUnits"
            ("Id", "JobId", "UnitKey", "Status", "Cursor", "ProcessedRecords", "FailedRecords",
             "AttemptCount", "NextAttemptAt", "LastError", "LeaseOwner", "LeaseExpiresAt",
             "FencingToken", "CreatedAt", "StartedAt", "CompletedAt", "UpdatedAt")
        VALUES
            (@Id, @JobId, @UnitKey, @Status, @Cursor, @ProcessedRecords, @FailedRecords,
             @AttemptCount, @NextAttemptAt, @LastError, @LeaseOwner, @LeaseExpiresAt,
             @FencingToken, @CreatedAt, @StartedAt, @CompletedAt, @UpdatedAt);
        """;

    private const string LockImportScopeSql = """
        SELECT pg_advisory_xact_lock(hashtext(@ImportType));
        """;

    private const string OverlapSql = """
        SELECT EXISTS (
            SELECT 1
            FROM "ImportWorkUnits" AS unit
            JOIN "ImportJobs" AS job ON job."Id" = unit."JobId"
            WHERE job."Status" IN ('Pending', 'Running')
              AND job."ImportType" = @ImportType
              AND unit."UnitKey" = ANY(@UnitKeys)
        );
        """;

    private const string SelectJobSql = """
        SELECT *
        FROM "ImportJobs"
        WHERE "Id" = @Id;
        """;

    private const string SelectRecoverableIdsSql = """
        SELECT job."Id"
        FROM "ImportJobs" AS job
        WHERE job."Status" IN ('Pending', 'Running')
          AND EXISTS (
              SELECT 1
              FROM "ImportWorkUnits" AS unit
              WHERE unit."JobId" = job."Id"
                AND (
                    (unit."Status" = 'Pending'
                        AND (unit."NextAttemptAt" IS NULL OR unit."NextAttemptAt" <= now()))
                    OR (unit."Status" = 'Running' AND unit."LeaseExpiresAt" < now())
                )
          )
        ORDER BY job."CreatedAt", job."Id"
        LIMIT @Limit;
        """;

    private const string MarkRunningSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Running',
            "StartedAt" = COALESCE("StartedAt", now()),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "Status" IN ('Pending', 'Running');
        """;

    private const string CancellationRequestedSql = """
        SELECT COALESCE(
            (SELECT "Status" = 'Cancelled' FROM "ImportJobs" WHERE "Id" = @Id),
            false);
        """;

    private const string CancelWorkUnitsSql = """
        UPDATE "ImportWorkUnits"
        SET "Status" = 'Cancelled',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "CompletedAt" = COALESCE("CompletedAt", now()),
            "UpdatedAt" = now()
        WHERE "JobId" = @Id
          AND "Status" IN ('Pending', 'Running')
          AND EXISTS (
              SELECT 1 FROM "ImportJobs"
              WHERE "Id" = @Id AND "Status" IN ('Pending', 'Running')
          );
        """;

    private const string CancelJobSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Cancelled',
            "CompletedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "Status" IN ('Pending', 'Running')
          AND @HadActiveUnits;
        """;

    private const string FailWorkUnitsSql = """
        UPDATE "ImportWorkUnits"
        SET "Status" = 'Failed',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "CompletedAt" = COALESCE("CompletedAt", now()),
            "LastError" = @Error,
            "UpdatedAt" = now()
        WHERE "JobId" = @Id
          AND "Status" IN ('Pending', 'Running');
        """;

    private const string FailJobSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Failed',
            "LastError" = @Error,
            "CompletedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "Status" IN ('Pending', 'Running');
        """;

    public async Task<bool> CreateAsync(
        ImportJob job,
        IReadOnlyCollection<ImportWorkUnit> workUnits,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            LockImportScopeSql,
            new { job.ImportType },
            transaction,
            cancellationToken: cancellationToken));

        var overlaps = await connection.QuerySingleAsync<bool>(new CommandDefinition(
            OverlapSql,
            new { job.ImportType, UnitKeys = workUnits.Select(unit => unit.UnitKey).ToArray() },
            transaction,
            cancellationToken: cancellationToken));

        if (overlaps)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            InsertJobSql,
            new
            {
                job.Id,
                job.ImportType,
                Status = job.Status.ToString(),
                ConflictStrategy = job.ConflictStrategy.ToString(),
                job.BatchSize,
                job.MaxAttempts,
                job.LeaseDurationSeconds,
                job.TotalProcessedRecords,
                job.TotalFailedRecords,
                job.LastError,
                job.CreatedAt,
                job.StartedAt,
                job.CompletedAt,
                job.UpdatedAt
            },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var workUnit in workUnits)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertWorkUnitSql,
                new
                {
                    workUnit.Id,
                    workUnit.JobId,
                    workUnit.UnitKey,
                    Status = workUnit.Status.ToString(),
                    workUnit.Cursor,
                    workUnit.ProcessedRecords,
                    workUnit.FailedRecords,
                    workUnit.AttemptCount,
                    workUnit.NextAttemptAt,
                    workUnit.LastError,
                    workUnit.LeaseOwner,
                    workUnit.LeaseExpiresAt,
                    workUnit.FencingToken,
                    workUnit.CreatedAt,
                    workUnit.StartedAt,
                    workUnit.CompletedAt,
                    workUnit.UpdatedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ImportJob>(new CommandDefinition(
            SelectJobSql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            SelectRecoverableIdsSql,
            new { Limit = limit },
            cancellationToken: cancellationToken));

        return ids.AsList();
    }

    public async Task<bool> MarkRunningAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            MarkRunningSql,
            new { Id = id },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> IsCancellationRequestedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleAsync<bool>(new CommandDefinition(
            CancellationRequestedSql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var cancelledUnits = await connection.ExecuteAsync(new CommandDefinition(
            CancelWorkUnitsSql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));

        var cancelledJob = await connection.ExecuteAsync(new CommandDefinition(
            CancelJobSql,
            new { Id = id, HadActiveUnits = cancelledUnits > 0 },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return cancelledJob > 0;
    }

    public async Task FailAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            FailWorkUnitsSql,
            new { Id = id, Error = error },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            FailJobSql,
            new { Id = id, Error = error },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }
}
