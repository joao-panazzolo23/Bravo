using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class ScopeCandidateUniqueKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candidates_ElectionId_TseCandidateSequential",
                table: "Candidates");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_StateId_OfficeId_TseCandidateSequenti~",
                table: "Candidates",
                columns: new[] { "ElectionId", "StateId", "OfficeId", "TseCandidateSequential", "BallotNumber", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candidates_ElectionId_StateId_OfficeId_TseCandidateSequenti~",
                table: "Candidates");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_TseCandidateSequential",
                table: "Candidates",
                columns: new[] { "ElectionId", "TseCandidateSequential" },
                unique: true);
        }
    }
}
