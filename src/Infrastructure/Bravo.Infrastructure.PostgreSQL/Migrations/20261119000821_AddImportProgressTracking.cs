using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddImportProgressTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "InsertedRecords",
                table: "ImportWorkUnits",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProgressUpdatedAt",
                table: "ImportWorkUnits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RejectedRecords",
                table: "ImportWorkUnits",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "ImportWorkUnits",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<long>(
                name: "TotalRecords",
                table: "ImportWorkUnits",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedRecords",
                table: "ImportWorkUnits",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsertedRecords",
                table: "ImportWorkUnits");

            migrationBuilder.DropColumn(
                name: "ProgressUpdatedAt",
                table: "ImportWorkUnits");

            migrationBuilder.DropColumn(
                name: "RejectedRecords",
                table: "ImportWorkUnits");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ImportWorkUnits");

            migrationBuilder.DropColumn(
                name: "TotalRecords",
                table: "ImportWorkUnits");

            migrationBuilder.DropColumn(
                name: "UpdatedRecords",
                table: "ImportWorkUnits");
        }
    }
}
