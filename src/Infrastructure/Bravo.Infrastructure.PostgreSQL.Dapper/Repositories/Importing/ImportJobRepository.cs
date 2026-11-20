using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

///this one could be a EF Core repo
internal sealed class ImportJobRepository(NpgsqlDataSource dataSource) : IImportJobRepository
{
    private const string LockImportScopeSql = """
        SELECT pg_advisory_xact_lock(hashtext('ImportJobs'));
        """;

    private const string OverlapSql = """
        SELECT EXISTS (
            SELECT 1
            FROM "ImportJobs"
            WHERE "Status" IN ('Pending', 'Running')
              AND "Source" = @Source
              AND "Scope" = @Scope
        );
        """;

    private const string InsertSql = """
        INSERT INTO "ImportJobs"
            ("Id", "Source", "Scope", "Status", "BatchSize", "MaxAttempts", "MaxParallelism",
             "LeaseDurationSeconds", "AttemptCount", "Stage", "TotalOperations",
             "CompletedOperations", "FailedOperations", "TotalProcessedRecords",
             "TotalFailedRecords", "CurrentOperation", "LastError", "LeaseOwner",
             "LeaseExpiresAt", "NextAttemptAt", "ProgressUpdatedAt", "StartedAt",
             "CompletedAt", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @Source, @Scope, @Status, @BatchSize, @MaxAttempts, @MaxParallelism,
             @LeaseDurationSeconds, @AttemptCount, @Stage, @TotalOperations,
             @CompletedOperations, @FailedOperations, @TotalProcessedRecords,
             @TotalFailedRecords, @CurrentOperation, @LastError, @LeaseOwner,
             @LeaseExpiresAt, @NextAttemptAt, @ProgressUpdatedAt, @StartedAt,
             @CompletedAt, @CreatedAt, @UpdatedAt);
        """;

    private const string SelectSql = """
        SELECT *
        FROM "ImportJobs"
        WHERE "Id" = @Id;
        """;

    private const string SelectRecoverableIdsSql = """
        SELECT "Id"
        FROM "ImportJobs"
        WHERE ("Status" = 'Pending'
                AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now()))
           OR ("Status" = 'Running' AND "LeaseExpiresAt" < now())
        ORDER BY "CreatedAt", "Id"
        LIMIT @Limit;
        """;

    private const string ClaimSql = """
        WITH claimable AS (
            SELECT "Id"
            FROM "ImportJobs"
            WHERE "Id" = @Id
              AND (("Status" = 'Pending'
                    AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now()))
                OR ("Status" = 'Running' AND "LeaseExpiresAt" < now()))
            FOR UPDATE SKIP LOCKED
        )
        UPDATE "ImportJobs" AS job
        SET "Status" = 'Running',
            "LeaseOwner" = @WorkerId,
            "LeaseExpiresAt" = now() + make_interval(secs => @LeaseSeconds),
            "AttemptCount" = job."AttemptCount" + 1,
            "StartedAt" = COALESCE(job."StartedAt", now()),
            "CompletedAt" = NULL,
            "NextAttemptAt" = NULL,
            "Stage" = 'Initializing',
            "TotalOperations" = 0,
            "CompletedOperations" = 0,
            "FailedOperations" = 0,
            "TotalProcessedRecords" = 0,
            "TotalFailedRecords" = 0,
            "CurrentOperation" = NULL,
            "LastError" = NULL,
            "UpdatedAt" = now()
        FROM claimable
        WHERE job."Id" = claimable."Id"
        RETURNING job.*;
        """;

    private const string RenewLeaseSql = """
        UPDATE "ImportJobs"
        SET "LeaseExpiresAt" = now() + make_interval(secs => @LeaseSeconds),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running';
        """;

    private const string ReportProgressSql = """
        UPDATE "ImportJobs"
        SET "Stage" = @Stage,
            "TotalOperations" = @TotalOperations,
            "CompletedOperations" = @CompletedOperations,
            "FailedOperations" = @FailedOperations,
            "TotalProcessedRecords" = @ProcessedRecords,
            "TotalFailedRecords" = @FailedRecords,
            "CurrentOperation" = @CurrentOperation,
            "ProgressUpdatedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running';
        """;

    private const string CompleteSql = """
        UPDATE "ImportJobs"
        SET "Status" = @Status,
            "Stage" = 'Completed',
            "LastError" = @Error,
            "CurrentOperation" = NULL,
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "NextAttemptAt" = NULL,
            "CompletedAt" = now(),
            "ProgressUpdatedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running';
        """;

    private const string FailSql = """
        UPDATE "ImportJobs"
        SET "Status" = CASE WHEN @RetryAt::timestamptz IS NULL THEN 'Failed' ELSE 'Pending' END,
            "LastError" = @Error,
            "CurrentOperation" = NULL,
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "NextAttemptAt" = @RetryAt::timestamptz,
            "CompletedAt" = CASE WHEN @RetryAt::timestamptz IS NULL THEN now() ELSE NULL END,
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running';
        """;

    private const string CancelSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Cancelled',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "CurrentOperation" = NULL,
            "CompletedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "Status" IN ('Pending', 'Running');
        """;

    private const string RequeueSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Pending',
            "AttemptCount" = 0,
            "NextAttemptAt" = NULL,
            "CompletedAt" = NULL,
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "LastError" = NULL,
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "Status" = 'Failed'
          AND NOT EXISTS (
              SELECT 1
              FROM "ImportJobs" AS active
              WHERE active."Status" IN ('Pending', 'Running')
                AND active."Source" = "ImportJobs"."Source"
                AND active."Scope" = "ImportJobs"."Scope"
          );
        """;

    private const string ReleaseSql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Pending',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "NextAttemptAt" = NULL,
            "CurrentOperation" = NULL,
            "LastError" = COALESCE(@Reason::text, "LastError"),
            "UpdatedAt" = now()
        WHERE "Id" = @Id
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running';
        """;

    private const string ReleaseOwnedBySql = """
        UPDATE "ImportJobs"
        SET "Status" = 'Pending',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "NextAttemptAt" = NULL,
            "CurrentOperation" = NULL,
            "LastError" = 'Recovered after an unclean shutdown.',
            "UpdatedAt" = now()
        WHERE "Status" = 'Running'
          AND "LeaseOwner" LIKE @OwnerPrefix;
        """;

    public async Task<bool> CreateAsync(
        ImportJob job,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            LockImportScopeSql,
            transaction: transaction,
            cancellationToken: cancellationToken));

        var overlaps = await connection.QuerySingleAsync<bool>(new CommandDefinition(
            OverlapSql,
            new { job.Source, job.Scope },
            transaction,
            cancellationToken: cancellationToken));

        if (overlaps)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            Parameters(job),
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ImportJob>(new CommandDefinition(
            SelectSql,
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

    public async Task<ImportJob?> ClaimAsync(
        Guid id,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ImportJob>(new CommandDefinition(
            ClaimSql,
            new { Id = id, WorkerId = workerId, LeaseSeconds = (int)leaseDuration.TotalSeconds },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RenewLeaseAsync(
        Guid id,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            RenewLeaseSql,
            new { Id = id, WorkerId = workerId, LeaseSeconds = (int)leaseDuration.TotalSeconds },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> ReportProgressAsync(
        Guid id,
        string workerId,
        ImportJobProgress progress,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            ReportProgressSql,
            new
            {
                Id = id,
                WorkerId = workerId,
                Stage = progress.Stage.ToString(),
                progress.TotalOperations,
                progress.CompletedOperations,
                progress.FailedOperations,
                progress.ProcessedRecords,
                progress.FailedRecords,
                progress.CurrentOperation
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task CompleteAsync(
        Guid id,
        string workerId,
        ImportJobStatus status,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            CompleteSql,
            new { Id = id, WorkerId = workerId, Status = status.ToString(), Error = error },
            cancellationToken: cancellationToken));
    }

    public async Task FailAsync(
        Guid id,
        string workerId,
        string error,
        DateTime? retryAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            FailSql,
            new { Id = id, WorkerId = workerId, Error = error, RetryAt = retryAt },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            CancelSql,
            new { Id = id },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            RequeueSql,
            new { Id = id },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task ReleaseAsync(
        Guid id,
        string workerId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            ReleaseSql,
            new { Id = id, WorkerId = workerId, Reason = reason },
            cancellationToken: cancellationToken));
    }

    public async Task<int> ReleaseOwnedByAsync(
        string ownerPrefix,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            ReleaseOwnedBySql,
            new { OwnerPrefix = ownerPrefix + "%" },
            cancellationToken: cancellationToken));
    }

    private static object Parameters(ImportJob job) => new
    {
        job.Id,
        job.Source,
        job.Scope,
        Status = job.Status.ToString(),
        job.BatchSize,
        job.MaxAttempts,
        job.MaxParallelism,
        job.LeaseDurationSeconds,
        job.AttemptCount,
        Stage = job.Stage.ToString(),
        job.TotalOperations,
        job.CompletedOperations,
        job.FailedOperations,
        job.TotalProcessedRecords,
        job.TotalFailedRecords,
        job.CurrentOperation,
        job.LastError,
        job.LeaseOwner,
        job.LeaseExpiresAt,
        job.NextAttemptAt,
        job.ProgressUpdatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.CreatedAt,
        job.UpdatedAt
    };
}
