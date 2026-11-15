using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddElectoralAndImportReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportCheckpoints_ExecutionId_DatasetKey",
                table: "ImportCheckpoints");

            migrationBuilder.DropIndex(
                name: "IX_DatasetProvenances_Provider_DatasetIdentifier",
                table: "DatasetProvenances");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "ImportExecutions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchesCommitted",
                table: "ImportExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "BytesDownloaded",
                table: "ImportExecutions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeartbeatAt",
                table: "ImportExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "ImportExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordsRejected",
                table: "ImportExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecordsSkipped",
                table: "ImportExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryAttempts",
                table: "ImportExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetKey",
                table: "ImportCheckpoints",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetIdentifier",
                table: "DatasetProvenances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "DatasetProvenances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceLastModifiedAt",
                table: "DatasetProvenances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeKey",
                table: "ImportExecutions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                computedColumnSql: "COALESCE(\"Scope\", '')",
                stored: true);

            migrationBuilder.CreateTable(
                name: "Elections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TypeCode = table.Column<int>(type: "integer", nullable: false),
                    TypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ElectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectoralZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectoralZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectoralZones_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportRejections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RecordReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayloadSample = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRejections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRejections_ImportExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "ImportExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MunicipalityTseCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IbgeCode = table.Column<int>(type: "integer", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MesoRegionName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MicroRegionName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunicipalityTseCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MunicipalityTseCodes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MunicipalityTseCodes_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PoliticalParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Acronym = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticalParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PollingPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingPlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingPlaces_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollingSectionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectoralContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollingSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Votes = table.Column<int>(type: "integer", nullable: false),
                    ProvenanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingSectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingSectionResults_DatasetProvenances_ProvenanceId",
                        column: x => x.ProvenanceId,
                        principalTable: "DatasetProvenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PollingSectionStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectoralContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollingSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleVoters = table.Column<int>(type: "integer", nullable: true),
                    Attendees = table.Column<int>(type: "integer", nullable: true),
                    Absentees = table.Column<int>(type: "integer", nullable: true),
                    NominalVotes = table.Column<int>(type: "integer", nullable: true),
                    BlankVotes = table.Column<int>(type: "integer", nullable: true),
                    NullVotes = table.Column<int>(type: "integer", nullable: true),
                    PartyVotes = table.Column<int>(type: "integer", nullable: true),
                    ProvenanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingSectionStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingSectionStatistics_DatasetProvenances_ProvenanceId",
                        column: x => x.ProvenanceId,
                        principalTable: "DatasetProvenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseSequential = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    BallotName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    OfficeCode = table.Column<int>(type: "integer", nullable: false),
                    OfficeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PoliticalPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StateId = table.Column<Guid>(type: "uuid", nullable: true),
                    SituationCode = table.Column<int>(type: "integer", nullable: true),
                    SituationDescription = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Candidates_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Candidates_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectionRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionRounds_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollingSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollingPlaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingSections_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollingSections_PollingPlaces_PollingPlaceId",
                        column: x => x.PollingPlaceId,
                        principalTable: "PollingPlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollingSections_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectoralContests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeCode = table.Column<int>(type: "integer", nullable: false),
                    OfficeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectoralContests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectoralContests_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectoralContests_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ImportExecutions_ActiveScope",
                table: "ImportExecutions",
                columns: new[] { "OperationType", "ScopeKey" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Running', 'CancellationRequested')");

            migrationBuilder.CreateIndex(
                name: "IX_ImportCheckpoints_ExecutionId_DatasetKey",
                table: "ImportCheckpoints",
                columns: new[] { "ExecutionId", "DatasetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetProvenances_Provider_DatasetIdentifier_FetchedAt",
                table: "DatasetProvenances",
                columns: new[] { "Provider", "DatasetIdentifier", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_OfficeCode",
                table: "Candidates",
                columns: new[] { "ElectionId", "OfficeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_TseSequential",
                table: "Candidates",
                columns: new[] { "ElectionId", "TseSequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_PoliticalPartyId_ElectionId",
                table: "Candidates",
                columns: new[] { "PoliticalPartyId", "ElectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_StateId",
                table: "Candidates",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionRounds_ElectionId_Number",
                table: "ElectionRounds",
                columns: new[] { "ElectionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elections_TseCode",
                table: "Elections",
                column: "TseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elections_Year",
                table: "Elections",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralContests_ElectionRoundId_OfficeCode_StateId",
                table: "ElectoralContests",
                columns: new[] { "ElectionRoundId", "OfficeCode", "StateId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralContests_StateId",
                table: "ElectoralContests",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralZones_StateId_Number",
                table: "ElectoralZones",
                columns: new[] { "StateId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportRejections_ExecutionId_DatasetKey",
                table: "ImportRejections",
                columns: new[] { "ExecutionId", "DatasetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalityTseCodes_IbgeCode",
                table: "MunicipalityTseCodes",
                column: "IbgeCode");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalityTseCodes_MunicipalityId",
                table: "MunicipalityTseCodes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalityTseCodes_StateId_TseCode",
                table: "MunicipalityTseCodes",
                columns: new[] { "StateId", "TseCode" });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalityTseCodes_TseCode",
                table: "MunicipalityTseCodes",
                column: "TseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoliticalParties_Number",
                table: "PoliticalParties",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaces_MunicipalityId_ZoneNumber_TseCode",
                table: "PollingPlaces",
                columns: new[] { "MunicipalityId", "ZoneNumber", "TseCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingSectionResults_ElectionId_CandidateId",
                table: "PollingSectionResults",
                columns: new[] { "ElectionId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PollingSectionResults_ElectionId_ElectionRoundId",
                table: "PollingSectionResults",
                columns: new[] { "ElectionId", "ElectionRoundId" });

            migrationBuilder.CreateIndex(
                name: "IX_PollingSectionResults_ProvenanceId",
                table: "PollingSectionResults",
                column: "ProvenanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PollingSectionResults_Contest_Section_Candidate",
                table: "PollingSectionResults",
                columns: new[] { "ElectoralContestId", "PollingSectionId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingSections_MunicipalityId",
                table: "PollingSections",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingSections_PollingPlaceId",
                table: "PollingSections",
                column: "PollingPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingSections_StateId_ZoneNumber_Number",
                table: "PollingSections",
                columns: new[] { "StateId", "ZoneNumber", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingSectionStatistics_ElectionId_ElectionRoundId",
                table: "PollingSectionStatistics",
                columns: new[] { "ElectionId", "ElectionRoundId" });

            migrationBuilder.CreateIndex(
                name: "IX_PollingSectionStatistics_ProvenanceId",
                table: "PollingSectionStatistics",
                column: "ProvenanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PollingSectionStatistics_Contest_Section",
                table: "PollingSectionStatistics",
                columns: new[] { "ElectoralContestId", "PollingSectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "ElectoralContests");

            migrationBuilder.DropTable(
                name: "ElectoralZones");

            migrationBuilder.DropTable(
                name: "ImportRejections");

            migrationBuilder.DropTable(
                name: "MunicipalityTseCodes");

            migrationBuilder.DropTable(
                name: "PoliticalParties");

            migrationBuilder.DropTable(
                name: "PollingSectionResults");

            migrationBuilder.DropTable(
                name: "PollingSections");

            migrationBuilder.DropTable(
                name: "PollingSectionStatistics");

            migrationBuilder.DropTable(
                name: "ElectionRounds");

            migrationBuilder.DropTable(
                name: "PollingPlaces");

            migrationBuilder.DropTable(
                name: "Elections");

            migrationBuilder.DropIndex(
                name: "UX_ImportExecutions_ActiveScope",
                table: "ImportExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ImportCheckpoints_ExecutionId_DatasetKey",
                table: "ImportCheckpoints");

            migrationBuilder.DropIndex(
                name: "IX_DatasetProvenances_Provider_DatasetIdentifier_FetchedAt",
                table: "DatasetProvenances");

            migrationBuilder.DropColumn(
                name: "ScopeKey",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "BatchesCommitted",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "BytesDownloaded",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "HeartbeatAt",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "RecordsRejected",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "RecordsSkipped",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "RecoveryAttempts",
                table: "ImportExecutions");

            migrationBuilder.DropColumn(
                name: "ETag",
                table: "DatasetProvenances");

            migrationBuilder.DropColumn(
                name: "SourceLastModifiedAt",
                table: "DatasetProvenances");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "ImportExecutions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetKey",
                table: "ImportCheckpoints",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "DatasetIdentifier",
                table: "DatasetProvenances",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_ImportCheckpoints_ExecutionId_DatasetKey",
                table: "ImportCheckpoints",
                columns: new[] { "ExecutionId", "DatasetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetProvenances_Provider_DatasetIdentifier",
                table: "DatasetProvenances",
                columns: new[] { "Provider", "DatasetIdentifier" });
        }
    }
}
