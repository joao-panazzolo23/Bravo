using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportFailedRecords_ImportWorkUnits_WorkUnitId",
                table: "ImportFailedRecords");

            migrationBuilder.DropTable(
                name: "ImportWorkUnits");

            migrationBuilder.DropIndex(
                name: "IX_ImportFailedRecords_WorkUnitId_SourceIdentifier",
                table: "ImportFailedRecords");

            migrationBuilder.DropColumn(
                name: "WorkUnitId",
                table: "ImportFailedRecords");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletedOperations",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentOperation",
                table: "ImportJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedOperations",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "ImportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "ImportJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxParallelism",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "ImportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProgressUpdatedAt",
                table: "ImportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ImportJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ImportJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "ImportJobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalOperations",
                table: "ImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Jobs created by the work-unit implementation have no source or scope, so they
            // cannot be executed by the new worker. Retire them instead of leaving them
            // Pending, where the overlap index and the recovery query would treat them as
            // runnable forever.
            migrationBuilder.Sql("""
                UPDATE "ImportJobs"
                SET "Source" = 'LEGACY',
                    "Scope" = "Id"::text,
                    "Status" = 'Failed',
                    "Stage" = 'Pending',
                    "LastError" = 'Retired by the job-level import migration; start a new import.'
                WHERE "Source" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_LeaseExpiresAt",
                table: "ImportJobs",
                column: "LeaseExpiresAt",
                filter: "\"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_NextAttemptAt",
                table: "ImportJobs",
                column: "NextAttemptAt",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_Source_Scope",
                table: "ImportJobs",
                columns: new[] { "Source", "Scope" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailedRecords_JobId_SourceIdentifier",
                table: "ImportFailedRecords",
                columns: new[] { "JobId", "SourceIdentifier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_LeaseExpiresAt",
                table: "ImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_NextAttemptAt",
                table: "ImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_Source_Scope",
                table: "ImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ImportFailedRecords_JobId_SourceIdentifier",
                table: "ImportFailedRecords");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "CompletedOperations",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "CurrentOperation",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "FailedOperations",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "MaxParallelism",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "ProgressUpdatedAt",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "TotalOperations",
                table: "ImportJobs");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkUnitId",
                table: "ImportFailedRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ImportWorkUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cursor = table.Column<string>(type: "text", nullable: true),
                    FailedRecords = table.Column<long>(type: "bigint", nullable: false),
                    FencingToken = table.Column<long>(type: "bigint", nullable: false),
                    InsertedRecords = table.Column<long>(type: "bigint", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedRecords = table.Column<long>(type: "bigint", nullable: false),
                    ProgressUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedRecords = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalRecords = table.Column<long>(type: "bigint", nullable: true),
                    UnitKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedRecords = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportWorkUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportWorkUnits_ImportJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportFailedRecords_WorkUnitId_SourceIdentifier",
                table: "ImportFailedRecords",
                columns: new[] { "WorkUnitId", "SourceIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportWorkUnits_JobId_Status_NextAttemptAt_CreatedAt",
                table: "ImportWorkUnits",
                columns: new[] { "JobId", "Status", "NextAttemptAt", "CreatedAt" },
                filter: "\"Status\" IN ('Pending', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_ImportWorkUnits_JobId_UnitKey",
                table: "ImportWorkUnits",
                columns: new[] { "JobId", "UnitKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportWorkUnits_LeaseExpiresAt",
                table: "ImportWorkUnits",
                column: "LeaseExpiresAt",
                filter: "\"Status\" = 'Running'");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportFailedRecords_ImportWorkUnits_WorkUnitId",
                table: "ImportFailedRecords",
                column: "WorkUnitId",
                principalTable: "ImportWorkUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
