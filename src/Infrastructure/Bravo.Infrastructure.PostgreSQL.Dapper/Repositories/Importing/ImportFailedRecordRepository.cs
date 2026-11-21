using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal sealed class ImportFailedRecordRepository(NpgsqlDataSource dataSource)
    : IImportFailedRecordRepository
{
    private const string SelectByJobSql = """
        SELECT *
        FROM "ImportFailedRecords"
        WHERE "JobId" = @JobId
        ORDER BY "CreatedAt" DESC, "Id"
        LIMIT @Limit;
        """;

    // Dead letters used to be serialized to JSON and parsed back by jsonb_to_recordset. They now
    // stream into a temporary table with binary COPY and merge with one INSERT ... SELECT.
    private static readonly BulkStagingTable Staging = new(
        """
        CREATE TEMP TABLE staging_import_failed_records (
            "Id" uuid,
            "JobId" uuid,
            "SourceIdentifier" varchar(500),
            "Payload" text,
            "ErrorCategory" varchar(50),
            "ErrorDescription" text,
            "AttemptCount" integer,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_import_failed_records
            ("Id", "JobId", "SourceIdentifier", "Payload", "ErrorCategory",
             "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // AttemptCount deliberately has no no-op guard: incrementing it is the observable effect of
    // recording the same failure again, so skipping the update would lose retry history. A batch
    // that repeats one source identifier collapses to its last staged row first, because
    // PostgreSQL rejects an upsert that would affect one row twice.
    private const string MergeSql = """
        INSERT INTO "ImportFailedRecords" AS target
            ("Id", "JobId", "SourceIdentifier", "Payload", "ErrorCategory",
             "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "JobId", "SourceIdentifier", "Payload", "ErrorCategory",
            "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("JobId", "SourceIdentifier")
                   "Id", "JobId", "SourceIdentifier", "Payload", "ErrorCategory",
                   "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt"
            FROM staging_import_failed_records
            ORDER BY "JobId", "SourceIdentifier", "ctid" DESC
        ) AS staged
        ORDER BY "JobId", "SourceIdentifier"
        ON CONFLICT ("JobId", "SourceIdentifier") DO UPDATE SET
            "Payload" = EXCLUDED."Payload",
            "ErrorCategory" = EXCLUDED."ErrorCategory",
            "ErrorDescription" = EXCLUDED."ErrorDescription",
            "AttemptCount" = target."AttemptCount" + 1,
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    public async Task<IReadOnlyList<ImportFailedRecord>> ListByJobAsync(
        Guid jobId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var records = await connection.QueryAsync<ImportFailedRecord>(new CommandDefinition(
            SelectByJobSql,
            new { JobId = jobId, Limit = limit },
            cancellationToken: cancellationToken));

        return records.AsList();
    }

    public async Task UpsertAsync(
        Guid jobId,
        IReadOnlyCollection<ImportRejectedRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
            return;

        var entities = records
            .Select(record => ImportFailedRecord.Create(
                jobId,
                record.SourceIdentifier,
                record.Payload,
                record.Category,
                record.Error,
                attemptCount: 1))
            .ToArray();

        await BulkCopy.UpsertAsync(
            dataSource,
            Staging,
            entities,
            WriteAsync,
            MergeSql,
            cancellationToken);
    }

    private static async Task WriteAsync(
        NpgsqlBinaryImporter importer,
        ImportFailedRecord record,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(record.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(record.JobId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(record.SourceIdentifier, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(record.Payload, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(record.ErrorCategory.ToString(), NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteAsync(record.ErrorDescription, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(record.AttemptCount, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(record.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(record.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }
}
