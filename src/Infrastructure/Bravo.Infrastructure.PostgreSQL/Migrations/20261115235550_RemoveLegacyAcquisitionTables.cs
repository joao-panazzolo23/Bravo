using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyAcquisitionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatasetProvenances");

            migrationBuilder.DropTable(
                name: "ImportCheckpoints");

            migrationBuilder.DropTable(
                name: "ImportExecutions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecordsPersisted = table.Column<int>(type: "integer", nullable: false),
                    RecordsProcessed = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetProvenances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DatasetIdentifier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FormatDescription = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OriginalFilename = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetProvenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatasetProvenances_ImportExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "ImportExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckpointData = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CheckpointedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatasetKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordsCompleted = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportCheckpoints_ImportExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "ImportExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetProvenances_ExecutionId_DatasetIdentifier",
                table: "DatasetProvenances",
                columns: new[] { "ExecutionId", "DatasetIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetProvenances_Provider_DatasetIdentifier",
                table: "DatasetProvenances",
                columns: new[] { "Provider", "DatasetIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportCheckpoints_ExecutionId_DatasetKey",
                table: "ImportCheckpoints",
                columns: new[] { "ExecutionId", "DatasetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportExecutions_OperationType_Status",
                table: "ImportExecutions",
                columns: new[] { "OperationType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportExecutions_Status_CreatedAt",
                table: "ImportExecutions",
                columns: new[] { "Status", "CreatedAt" });
        }
    }
}
