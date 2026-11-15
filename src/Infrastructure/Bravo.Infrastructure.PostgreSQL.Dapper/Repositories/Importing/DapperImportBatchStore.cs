using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Exceptions;
using Bravo.Application.Importing.Models;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

public abstract class DapperImportBatchStore<TRecord>(NpgsqlDataSource dataSource)
    : IImportBatchStore<TRecord>
{
    private const string AdvanceCheckpointSql = """
        UPDATE "ImportWorkUnits"
        SET "Cursor" = @NextCursor,
            "ProcessedRecords" = "ProcessedRecords" + @ProcessedRecords,
            "FailedRecords" = "FailedRecords" + @FailedRecords,
            "Status" = CASE WHEN @IsFinalBatch THEN 'Completed' ELSE 'Running' END,
            "LeaseExpiresAt" = now() + make_interval(secs => @LeaseDurationSeconds),
            "CompletedAt" = CASE WHEN @IsFinalBatch THEN now() ELSE NULL END,
            "UpdatedAt" = now()
        WHERE "Id" = @WorkUnitId
          AND "FencingToken" = @FencingToken
          AND "LeaseOwner" = @WorkerId
          AND "Status" = 'Running'
          AND "LeaseExpiresAt" > now()
          AND EXISTS (
              SELECT 1
              FROM "ImportJobs"
              WHERE "Id" = @JobId
                AND "Status" = 'Running'
          );
        """;

    private const string InsertFailedRecordSql = """
        INSERT INTO "ImportFailedRecords"
            ("Id", "JobId", "WorkUnitId", "SourceIdentifier", "Payload",
             "ErrorCategory", "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt")
        VALUES
            (@Id, @JobId, @WorkUnitId, @SourceIdentifier, @Payload,
             @ErrorCategory, @ErrorDescription, @AttemptCount, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("WorkUnitId", "SourceIdentifier")
        DO UPDATE SET
            "Payload" = EXCLUDED."Payload",
            "ErrorCategory" = EXCLUDED."ErrorCategory",
            "ErrorDescription" = EXCLUDED."ErrorDescription",
            "AttemptCount" = "ImportFailedRecords"."AttemptCount" + 1,
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpdateJobTotalsSql = """
        UPDATE "ImportJobs"
        SET "TotalProcessedRecords" = "TotalProcessedRecords" + @ProcessedRecords,
            "TotalFailedRecords" = "TotalFailedRecords" + @FailedRecords,
            "UpdatedAt" = now()
        WHERE "Id" = @JobId;
        """;

    public async Task PersistBatchAsync(
        ImportBatchWriteContext context,
        IReadOnlyList<TRecord> records,
        IReadOnlyList<ImportRejectedRecord> rejectedRecords,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var advanceParameters = new
        {
            context.WorkUnitId,
            context.JobId,
            context.FencingToken,
            context.WorkerId,
            context.NextCursor,
            context.IsFinalBatch,
            context.LeaseDurationSeconds,
            ProcessedRecords = records.Count,
            FailedRecords = rejectedRecords.Count
        };

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            AdvanceCheckpointSql,
            advanceParameters,
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new ImportLeaseLostException(
                $"Work unit '{context.WorkUnitId}' is no longer owned by worker '{context.WorkerId}'.");
        }

        if (records.Count > 0)
            await PersistRecordsAsync(connection, transaction, records, cancellationToken);

        if (rejectedRecords.Count > 0)
            await PersistRejectedRecordsAsync(connection, transaction, context, rejectedRecords, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateJobTotalsSql,
            new
            {
                context.JobId,
                ProcessedRecords = records.Count,
                FailedRecords = rejectedRecords.Count
            },
            transaction,
            cancellationToken: cancellationToken));

        if (context.IsFinalBatch)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ImportJobSql.RefreshState,
                new { context.JobId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    protected abstract Task PersistRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<TRecord> records,
        CancellationToken cancellationToken);

    private static async Task PersistRejectedRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ImportBatchWriteContext context,
        IReadOnlyList<ImportRejectedRecord> rejectedRecords,
        CancellationToken cancellationToken)
    {
        foreach (var rejected in rejectedRecords)
        {
            var record = ImportFailedRecord.Create(
                context.JobId,
                context.WorkUnitId,
                rejected.SourceIdentifier,
                rejected.Payload,
                rejected.Category,
                rejected.Error,
                1);

            await connection.ExecuteAsync(new CommandDefinition(
                InsertFailedRecordSql,
                new
                {
                    record.Id,
                    record.JobId,
                    record.WorkUnitId,
                    record.SourceIdentifier,
                    record.Payload,
                    ErrorCategory = record.ErrorCategory.ToString(),
                    record.ErrorDescription,
                    record.AttemptCount,
                    record.CreatedAt,
                    record.UpdatedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }
}
