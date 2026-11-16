using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddImportDatasetVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportDatasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Dataset = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ElectionYear = table.Column<int>(type: "integer", nullable: true),
                    LayoutVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportDatasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportDatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatasetVersions_ImportDatasets_ImportDatasetId",
                        column: x => x.ImportDatasetId,
                        principalTable: "ImportDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DatasetVersions_ImportJobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "ImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetVersions_ImportDatasetId_Scope",
                table: "DatasetVersions",
                columns: new[] { "ImportDatasetId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetVersions_ImportJobId",
                table: "DatasetVersions",
                column: "ImportJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DatasetVersions_Scope",
                table: "DatasetVersions",
                column: "Scope",
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_DatasetVersions_Status_CreatedAt",
                table: "DatasetVersions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportDatasets_Provider_Dataset_ElectionYear_Checksum",
                table: "ImportDatasets",
                columns: new[] { "Provider", "Dataset", "ElectionYear", "Checksum" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatasetVersions");

            migrationBuilder.DropTable(
                name: "ImportDatasets");
        }
    }
}
