namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing;

internal static class ImportJobSql
{
    public const string RefreshState = """
        UPDATE "ImportJobs" AS job
        SET "Status" = CASE
                WHEN EXISTS (
                    SELECT 1 FROM "ImportWorkUnits"
                    WHERE "JobId" = job."Id" AND "Status" IN ('Pending', 'Running')
                ) THEN 'Running'
                WHEN EXISTS (
                    SELECT 1 FROM "ImportWorkUnits"
                    WHERE "JobId" = job."Id" AND "Status" = 'Failed'
                ) THEN 'Failed'
                WHEN EXISTS (
                    SELECT 1 FROM "ImportWorkUnits"
                    WHERE "JobId" = job."Id" AND "Status" = 'Cancelled'
                ) THEN 'Cancelled'
                WHEN job."TotalFailedRecords" > 0 THEN 'CompletedWithErrors'
                ELSE 'Completed'
            END,
            "CompletedAt" = CASE
                WHEN EXISTS (
                    SELECT 1 FROM "ImportWorkUnits"
                    WHERE "JobId" = job."Id" AND "Status" IN ('Pending', 'Running')
                ) THEN NULL
                ELSE now()
            END,
            "UpdatedAt" = now()
        WHERE job."Id" = @JobId
          AND job."Status" = 'Running';
        """;
}
