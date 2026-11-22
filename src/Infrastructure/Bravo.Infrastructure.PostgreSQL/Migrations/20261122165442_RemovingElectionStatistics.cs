using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemovingElectionStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectionStatistics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectionStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Absentees = table.Column<long>(type: "bigint", nullable: true),
                    Attendees = table.Column<long>(type: "bigint", nullable: true),
                    BlankVotes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatasetVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleVoters = table.Column<long>(type: "bigint", nullable: true),
                    NullVotes = table.Column<long>(type: "bigint", nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionStatistics_DatasetVersions_DatasetVersionId",
                        column: x => x.DatasetVersionId,
                        principalTable: "DatasetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionStatistics_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionStatistics_ElectoralSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "ElectoralSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionStatistics_DatasetVersionId",
                table: "ElectionStatistics",
                column: "DatasetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionStatistics_ElectionRoundId_SectionId_DatasetVersion~",
                table: "ElectionStatistics",
                columns: new[] { "ElectionRoundId", "SectionId", "DatasetVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectionStatistics_SectionId",
                table: "ElectionStatistics",
                column: "SectionId");
        }
    }
}
