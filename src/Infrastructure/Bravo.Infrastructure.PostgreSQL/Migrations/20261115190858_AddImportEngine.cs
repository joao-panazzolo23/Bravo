using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddImportEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "ImportJobs"
                (
                    "Id" uuid NOT NULL,
                    "ImportType" character varying(100) NOT NULL,
                    "Status" character varying(32) NOT NULL,
                    "ConflictStrategy" character varying(32) NOT NULL,
                    "BatchSize" integer NOT NULL,
                    "MaxAttempts" integer NOT NULL,
                    "LeaseDurationSeconds" integer NOT NULL,
                    "TotalProcessedRecords" bigint NOT NULL DEFAULT 0,
                    "TotalFailedRecords" bigint NOT NULL DEFAULT 0,
                    "LastError" text NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "StartedAt" timestamp with time zone NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ImportJobs" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_ImportJobs_ImportType" CHECK (length("ImportType") > 0),
                    CONSTRAINT "CK_ImportJobs_Status" CHECK ("Status" IN ('Pending', 'Running', 'Completed', 'CompletedWithErrors', 'Failed', 'Cancelled')),
                    CONSTRAINT "CK_ImportJobs_ConflictStrategy" CHECK ("ConflictStrategy" IN ('DoNothing', 'Update', 'Fail')),
                    CONSTRAINT "CK_ImportJobs_BatchSize" CHECK ("BatchSize" > 0),
                    CONSTRAINT "CK_ImportJobs_MaxAttempts" CHECK ("MaxAttempts" > 0),
                    CONSTRAINT "CK_ImportJobs_LeaseDurationSeconds" CHECK ("LeaseDurationSeconds" > 0),
                    CONSTRAINT "CK_ImportJobs_Totals" CHECK ("TotalProcessedRecords" >= 0 AND "TotalFailedRecords" >= 0)
                );

                CREATE INDEX "IX_ImportJobs_Status_CreatedAt"
                    ON "ImportJobs" ("Status", "CreatedAt");

                CREATE TABLE "ImportWorkUnits"
                (
                    "Id" uuid NOT NULL,
                    "JobId" uuid NOT NULL,
                    "UnitKey" character varying(200) NOT NULL,
                    "Status" character varying(20) NOT NULL,
                    "Cursor" text NULL,
                    "ProcessedRecords" bigint NOT NULL DEFAULT 0,
                    "FailedRecords" bigint NOT NULL DEFAULT 0,
                    "AttemptCount" integer NOT NULL DEFAULT 0,
                    "NextAttemptAt" timestamp with time zone NULL,
                    "LastError" text NULL,
                    "LeaseOwner" character varying(200) NULL,
                    "LeaseExpiresAt" timestamp with time zone NULL,
                    "FencingToken" bigint NOT NULL DEFAULT 0,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "StartedAt" timestamp with time zone NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ImportWorkUnits" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ImportWorkUnits_ImportJobs_JobId"
                        FOREIGN KEY ("JobId") REFERENCES "ImportJobs" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "UQ_ImportWorkUnits_JobId_UnitKey" UNIQUE ("JobId", "UnitKey"),
                    CONSTRAINT "CK_ImportWorkUnits_UnitKey" CHECK (length("UnitKey") > 0),
                    CONSTRAINT "CK_ImportWorkUnits_Status" CHECK ("Status" IN ('Pending', 'Running', 'Completed', 'Failed', 'Cancelled')),
                    CONSTRAINT "CK_ImportWorkUnits_Counters" CHECK ("ProcessedRecords" >= 0 AND "FailedRecords" >= 0 AND "AttemptCount" >= 0 AND "FencingToken" >= 0),
                    CONSTRAINT "CK_ImportWorkUnits_RunningLease"
                        CHECK ("Status" <> 'Running' OR ("LeaseOwner" IS NOT NULL AND "LeaseExpiresAt" IS NOT NULL))
                );

                CREATE INDEX "IX_ImportWorkUnits_Claim"
                    ON "ImportWorkUnits" ("JobId", "Status", "NextAttemptAt", "CreatedAt")
                    WHERE "Status" IN ('Pending', 'Running');

                CREATE INDEX "IX_ImportWorkUnits_ExpiredLease"
                    ON "ImportWorkUnits" ("LeaseExpiresAt")
                    WHERE "Status" = 'Running';

                CREATE TABLE "ImportFailedRecords"
                (
                    "Id" uuid NOT NULL,
                    "JobId" uuid NOT NULL,
                    "WorkUnitId" uuid NOT NULL,
                    "SourceIdentifier" character varying(500) NOT NULL,
                    "Payload" text NULL,
                    "ErrorCategory" character varying(32) NOT NULL,
                    "ErrorDescription" text NOT NULL,
                    "AttemptCount" integer NOT NULL DEFAULT 1,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ImportFailedRecords" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ImportFailedRecords_ImportJobs_JobId"
                        FOREIGN KEY ("JobId") REFERENCES "ImportJobs" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_ImportFailedRecords_ImportWorkUnits_WorkUnitId"
                        FOREIGN KEY ("WorkUnitId") REFERENCES "ImportWorkUnits" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "UQ_ImportFailedRecords_WorkUnitId_SourceIdentifier"
                        UNIQUE ("WorkUnitId", "SourceIdentifier"),
                    CONSTRAINT "CK_ImportFailedRecords_SourceIdentifier" CHECK (length("SourceIdentifier") > 0),
                    CONSTRAINT "CK_ImportFailedRecords_ErrorCategory"
                        CHECK ("ErrorCategory" IN ('Validation', 'Deserialization', 'Source', 'Persistence', 'Infrastructure')),
                    CONSTRAINT "CK_ImportFailedRecords_AttemptCount" CHECK ("AttemptCount" > 0)
                );

                CREATE INDEX "IX_ImportFailedRecords_JobId_CreatedAt"
                    ON "ImportFailedRecords" ("JobId", "CreatedAt" DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "ImportFailedRecords";
                DROP TABLE IF EXISTS "ImportWorkUnits";
                DROP TABLE IF EXISTS "ImportJobs";
                """);
        }
    }
}
