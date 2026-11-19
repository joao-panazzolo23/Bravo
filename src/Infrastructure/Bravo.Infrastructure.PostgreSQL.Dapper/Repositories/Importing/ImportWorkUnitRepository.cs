using Bravo.Application.Importing.Contracts;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class ImportWorkUnitRepository(NpgsqlDataSource dataSource) : IImportWorkUnitRepository
{
    private const string ClaimNextSql = """
        WITH claimable AS (
            SELECT unit."Id"
            FROM "ImportWorkUnits" AS unit
            JOIN "ImportJobs" AS job ON job."Id" = unit."JobId"
            WHERE unit."JobId" = @JobId
              AND job."Status" = 'Running'
              AND (
                  (unit."Status" = 'Pending'
                      AND (unit."NextAttemptAt" IS NULL OR unit."NextAttemptAt" <= now()))
                  OR (unit."Status" = 'Running' AND unit."LeaseExpiresAt" < now())
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM "ImportWorkUnits" AS previous
                  WHERE previous."JobId" = unit."JobId"
                    AND (previous."CreatedAt", previous."Id") < (unit."CreatedAt", unit."Id")
                    AND previous."Status" IN ('Pending', 'Running')
              )
            ORDER BY unit."CreatedAt", unit."Id"
            FOR UPDATE OF unit SKIP LOCKED
            LIMIT 1
        )
        UPDATE "ImportWorkUnits" AS unit
        SET "Status" = 'Running',
            "Stage" = 'Initializing',
            "LeaseOwner" = @WorkerId,
            "LeaseExpiresAt" = now() + make_interval(secs => @LeaseSeconds),
            "FencingToken" = unit."FencingToken" + 1,
            "StartedAt" = COALESCE(unit."StartedAt", now()),
            "NextAttemptAt" = NULL,
            "UpdatedAt" = now()
        FROM claimable
        WHERE unit."Id" = claimable."Id"
        RETURNING unit.*;
        """;

    private const string RenewLeaseSql = """
        UPDATE "ImportWorkUnits"
        SET "LeaseExpiresAt" = now() + make_interval(secs => @LeaseSeconds),
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "LeaseOwner" = @WorkerId
          AND "FencingToken" = @FencingToken
          AND "Status" = 'Running'
          AND "LeaseExpiresAt" > now();
        """;

    private const string RecordAttemptSql = """
        UPDATE "ImportWorkUnits"
        SET "AttemptCount" = "AttemptCount" + 1,
            "LastError" = @Error,
            "NextAttemptAt" = @NextAttemptAt::timestamptz,
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "FencingToken" = @FencingToken
          AND "Status" = 'Running'
        RETURNING "AttemptCount";
        """;

    private const string FailWorkUnitSql = """
        UPDATE "ImportWorkUnits"
        SET "Status" = 'Failed',
            "Stage" = 'Failed',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "CompletedAt" = now(),
            "LastError" = @Error,
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "JobId" = @JobId
          AND "FencingToken" = @FencingToken
          AND "Status" = 'Running';
        """;

    private const string ReleaseWorkUnitSql = """
        UPDATE "ImportWorkUnits"
        SET "Status" = 'Pending',
            "Stage" = 'Pending',
            "LeaseOwner" = NULL,
            "LeaseExpiresAt" = NULL,
            "NextAttemptAt" = NULL,
            "LastError" = COALESCE(@Reason::text, "LastError"),
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "LeaseOwner" = @WorkerId
          AND "FencingToken" = @FencingToken
          AND "Status" = 'Running';
        """;

    private const string CompleteWorkUnitSql = """
        WITH completed AS (
            UPDATE "ImportWorkUnits"
            SET "Status" = 'Completed',
                "Stage" = 'Completed',
                "LeaseOwner" = NULL,
                "LeaseExpiresAt" = NULL,
                "NextAttemptAt" = NULL,
                "CompletedAt" = now(),
                "UpdatedAt" = now()
            WHERE "Id" = @WorkUnitId
              AND "FencingToken" = @FencingToken
              AND "Status" = 'Running'
            RETURNING "JobId"
        )
        UPDATE "ImportJobs" AS job
        SET "UpdatedAt" = now()
        FROM completed
        WHERE job."Id" = completed."JobId"
        RETURNING job."Id";
        """;

    private const string ReportProgressSql = """
        UPDATE "ImportWorkUnits"
        SET "Stage" = @Stage,
            "ProcessedRecords" = @ProcessedRecords,
            "InsertedRecords" = @InsertedRecords,
            "UpdatedRecords" = @UpdatedRecords,
            "RejectedRecords" = @RejectedRecords,
            "TotalRecords" = COALESCE(@TotalRecords, "TotalRecords"),
            "ProgressUpdatedAt" = now(),
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "FencingToken" = @FencingToken
          AND "Status" = 'Running';
        """;

    private const string SelectJobIdSql = """
        SELECT "JobId" FROM "ImportWorkUnits" WHERE "Id" = @WorkUnitId;
        """;

    private const string SelectByJobSql = """
        SELECT *
        FROM "ImportWorkUnits"
        WHERE "JobId" = @JobId
        ORDER BY "CreatedAt", "Id";
        """;

    private const string SelectFailedKeysSql = """
        SELECT "UnitKey"
        FROM "ImportWorkUnits"
        WHERE "JobId" = @JobId
          AND "Status" = 'Failed'
        ORDER BY "CreatedAt", "Id";
        """;

    public async Task<ImportWorkUnit?> ClaimNextAsync(
        Guid jobId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ImportWorkUnit>(new CommandDefinition(
            ClaimNextSql,
            new
            {
                JobId = jobId,
                WorkerId = workerId,
                LeaseSeconds = (int)leaseDuration.TotalSeconds
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RenewLeaseAsync(
        Guid workUnitId,
        string workerId,
        long fencingToken,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            RenewLeaseSql,
            new
            {
                WorkUnitId = workUnitId,
                WorkerId = workerId,
                FencingToken = fencingToken,
                LeaseSeconds = (int)leaseDuration.TotalSeconds
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<int> RecordAttemptAsync(
        Guid workUnitId,
        long fencingToken,
        string error,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            RecordAttemptSql,
            new
            {
                WorkUnitId = workUnitId,
                FencingToken = fencingToken,
                Error = error,
                NextAttemptAt = nextAttemptAt
            },
            cancellationToken: cancellationToken)) ?? 0;
    }

    public async Task<bool> FailAsync(
        Guid jobId,
        Guid workUnitId,
        long fencingToken,
        string error,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            FailWorkUnitSql,
            new { JobId = jobId, WorkUnitId = workUnitId, FencingToken = fencingToken, Error = error },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ImportJobSql.RefreshState,
                new { JobId = jobId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> CompleteAsync(
        Guid workUnitId,
        long fencingToken,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            CompleteWorkUnitSql,
            new { WorkUnitId = workUnitId, FencingToken = fencingToken },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            var jobId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
                SelectJobIdSql,
                new { WorkUnitId = workUnitId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                ImportJobSql.RefreshState,
                new { JobId = jobId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);

        return affected > 0;
    }

    public async Task<bool> ReleaseAsync(
        Guid workUnitId,
        string workerId,
        long fencingToken,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            ReleaseWorkUnitSql,
            new { WorkUnitId = workUnitId, WorkerId = workerId, FencingToken = fencingToken, Reason = reason },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> ReportProgressAsync(
        Guid workUnitId,
        long fencingToken,
        ImportStage stage,
        long processedRecords,
        long insertedRecords,
        long updatedRecords,
        long rejectedRecords,
        long? totalRecords,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            ReportProgressSql,
            new
            {
                WorkUnitId = workUnitId,
                FencingToken = fencingToken,
                Stage = stage.ToString(),
                ProcessedRecords = processedRecords,
                InsertedRecords = insertedRecords,
                UpdatedRecords = updatedRecords,
                RejectedRecords = rejectedRecords,
                TotalRecords = totalRecords
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<IReadOnlyList<ImportWorkUnit>> GetByJobAsync(        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var units = await connection.QueryAsync<ImportWorkUnit>(new CommandDefinition(
            SelectByJobSql,
            new { JobId = jobId },
            cancellationToken: cancellationToken));

        return units.AsList();
    }

    public async Task<IReadOnlyList<string>> GetFailedKeysAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var keys = await connection.QueryAsync<string>(new CommandDefinition(
            SelectFailedKeysSql,
            new { JobId = jobId },
            cancellationToken: cancellationToken));

        return keys.AsList();
    }
}
