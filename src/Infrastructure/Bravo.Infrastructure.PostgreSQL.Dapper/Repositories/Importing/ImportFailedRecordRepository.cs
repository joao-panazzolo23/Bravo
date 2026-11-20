using System.Text.Json;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Dapper;
using Npgsql;

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

    private const string UpsertSql = """
        INSERT INTO "ImportFailedRecords"
            ("Id", "JobId", "SourceIdentifier", "Payload", "ErrorCategory",
             "ErrorDescription", "AttemptCount", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."JobId", record."SourceIdentifier", record."Payload",
               record."ErrorCategory", record."ErrorDescription", record."AttemptCount",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "JobId" uuid, "SourceIdentifier" varchar(500), "Payload" text,
            "ErrorCategory" varchar(50), "ErrorDescription" text, "AttemptCount" integer,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("JobId", "SourceIdentifier") DO UPDATE SET
            "Payload" = EXCLUDED."Payload",
            "ErrorCategory" = EXCLUDED."ErrorCategory",
            "ErrorDescription" = EXCLUDED."ErrorDescription",
            "AttemptCount" = "ImportFailedRecords"."AttemptCount" + 1,
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

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(entities.Select(entity => new
        {
            entity.Id,
            entity.JobId,
            entity.SourceIdentifier,
            entity.Payload,
            ErrorCategory = entity.ErrorCategory.ToString(),
            entity.ErrorDescription,
            entity.AttemptCount,
            entity.CreatedAt,
            entity.UpdatedAt
        }));

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            cancellationToken: cancellationToken));
    }
}
