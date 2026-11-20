using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class FixUniqueIndexNullSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The stronger uniqueness constraint cannot be created while rows that the old
            // index allowed to duplicate still exist. Both remediation steps below are
            // lossless: duplicated result rows hold the same votes (they came from repeating
            // an idempotent import), and duplicated identities are the same person by the
            // definition the index uses, so candidates can be repointed to the surviving row.
            migrationBuilder.Sql(
                """
                UPDATE "Candidates" AS c
                SET "CandidateIdentityId" = keep."KeepId"
                FROM (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "FullName", "BirthDate" ORDER BY "Id") AS "KeepId"
                    FROM "CandidateIdentities"
                    WHERE "Cpf" IS NULL AND "BirthDate" IS NULL
                ) AS keep
                WHERE c."CandidateIdentityId" = keep."Id"
                  AND keep."Id" <> keep."KeepId";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "CandidateIdentities" AS victim
                USING (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "FullName", "BirthDate" ORDER BY "Id") AS rn
                    FROM "CandidateIdentities"
                    WHERE "Cpf" IS NULL AND "BirthDate" IS NULL
                ) AS ranked
                WHERE victim."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "ElectionResults" AS victim
                USING (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                                            "CandidateId", "PartyId", "TseBallotNumber",
                                            "DatasetVersionId"
                               ORDER BY "UpdatedAt" DESC, "Id") AS rn
                    FROM "ElectionResults"
                ) AS ranked
                WHERE victim."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId_VoteType~",
                table: "ElectionResults");

            migrationBuilder.DropIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId_VoteType~",
                table: "ElectionResults",
                columns: new[] { "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities",
                columns: new[] { "FullName", "BirthDate" },
                unique: true,
                filter: "\"Cpf\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId_VoteType~",
                table: "ElectionResults");

            migrationBuilder.DropIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId_VoteType~",
                table: "ElectionResults",
                columns: new[] { "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities",
                columns: new[] { "FullName", "BirthDate" },
                unique: true,
                filter: "\"Cpf\" IS NULL");
        }
    }
}
