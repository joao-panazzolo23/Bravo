using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIdentityWithoutCpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older imports inserted a new identity for every candidate without a CPF. Repoint
            // the candidates to the earliest identity in each (name, birth date) group and
            // delete the duplicates, otherwise the unique index below cannot be created.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "FullName", "BirthDate"
                               ORDER BY "CreatedAt", "Id") AS "KeepId"
                    FROM "CandidateIdentities"
                    WHERE "Cpf" IS NULL
                )
                UPDATE "Candidates" AS candidate
                SET "CandidateIdentityId" = ranked."KeepId"
                FROM ranked
                WHERE candidate."CandidateIdentityId" = ranked."Id"
                  AND ranked."Id" <> ranked."KeepId";
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "FullName", "BirthDate"
                               ORDER BY "CreatedAt", "Id") AS "KeepId"
                    FROM "CandidateIdentities"
                    WHERE "Cpf" IS NULL
                )
                DELETE FROM "CandidateIdentities" AS identity
                USING ranked
                WHERE identity."Id" = ranked."Id"
                  AND ranked."Id" <> ranked."KeepId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities",
                columns: new[] { "FullName", "BirthDate" },
                unique: true,
                filter: "\"Cpf\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CandidateIdentities_FullName_BirthDate",
                table: "CandidateIdentities");
        }
    }
}
