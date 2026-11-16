using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bravo.Infrastructure.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddElectoralDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Elections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TseElectionCode = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TypeCode = table.Column<int>(type: "integer", nullable: false),
                    TypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ElectionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectoralOffices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TseOfficeCode = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectoralOffices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectoralZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseZoneNumber = table.Column<int>(type: "integer", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectoralZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectoralZones_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectoralZones_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Federations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TseFederationNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Acronym = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Composition = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Federations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoliticalParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TsePartyNumber = table.Column<int>(type: "integer", nullable: false),
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
                name: "TseMunicipalityCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RawCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElectionYear = table.Column<int>(type: "integer", nullable: false),
                    MatchMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseMunicipalityCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TseMunicipalityCodes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TseMunicipalityCodes_States_StateId",
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
                    Round = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsRunoff = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "ElectoralSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseSectionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectoralSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectoralSections_ElectoralZones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "ElectoralZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectoralSections_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollingPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    TsePollingPlaceNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Neighborhood = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingPlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingPlaces_ElectoralZones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "ElectoralZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollingPlaces_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollingPlaces_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Coalitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TseCoalitionNumber = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Composition = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coalitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coalitions_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Coalitions_ElectoralOffices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "ElectoralOffices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Coalitions_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectionStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleVoters = table.Column<long>(type: "bigint", nullable: true),
                    Attendees = table.Column<long>(type: "bigint", nullable: true),
                    Absentees = table.Column<long>(type: "bigint", nullable: true),
                    BlankVotes = table.Column<long>(type: "bigint", nullable: true),
                    NullVotes = table.Column<long>(type: "bigint", nullable: true),
                    DatasetVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PollingPlaceSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollingPlaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollingPlaceSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollingPlaceSections_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollingPlaceSections_ElectoralSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "ElectoralSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollingPlaceSections_PollingPlaces_PollingPlaceId",
                        column: x => x.PollingPlaceId,
                        principalTable: "PollingPlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateIdentityId = table.Column<Guid>(type: "uuid", nullable: true),
                    TseCandidateSequential = table.Column<long>(type: "bigint", nullable: false),
                    BallotNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UrnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SocialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    FederationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoalitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Education = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Race = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Occupation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Candidates_CandidateIdentities_CandidateIdentityId",
                        column: x => x.CandidateIdentityId,
                        principalTable: "CandidateIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Candidates_Coalitions_CoalitionId",
                        column: x => x.CoalitionId,
                        principalTable: "Coalitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Candidates_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Candidates_ElectoralOffices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "ElectoralOffices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Candidates_Federations_FederationId",
                        column: x => x.FederationId,
                        principalTable: "Federations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Candidates_PoliticalParties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "PoliticalParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Candidates_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoteType = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TseBallotNumber = table.Column<int>(type: "integer", nullable: false),
                    Votes = table.Column<long>(type: "bigint", nullable: false),
                    DatasetVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionResults", x => x.Id);
                    table.CheckConstraint("CK_ElectionResults_BlankNullHaveNoReferences", "\"VoteType\" NOT IN (3, 4) OR (\"CandidateId\" IS NULL AND \"PartyId\" IS NULL)");
                    table.CheckConstraint("CK_ElectionResults_NominalRequiresCandidate", "\"VoteType\" <> 1 OR \"CandidateId\" IS NOT NULL");
                    table.CheckConstraint("CK_ElectionResults_PartyRequiresParty", "\"VoteType\" <> 2 OR \"PartyId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ElectionResults_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectionResults_DatasetVersions_DatasetVersionId",
                        column: x => x.DatasetVersionId,
                        principalTable: "DatasetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionResults_ElectionRounds_ElectionRoundId",
                        column: x => x.ElectionRoundId,
                        principalTable: "ElectionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionResults_ElectoralOffices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "ElectoralOffices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectionResults_ElectoralSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "ElectoralSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectionResults_PoliticalParties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "PoliticalParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateIdentities_Cpf",
                table: "CandidateIdentities",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_CandidateIdentityId",
                table: "Candidates",
                column: "CandidateIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_CoalitionId",
                table: "Candidates",
                column: "CoalitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_OfficeId_StateId",
                table: "Candidates",
                columns: new[] { "ElectionId", "OfficeId", "StateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_ElectionId_TseCandidateSequential",
                table: "Candidates",
                columns: new[] { "ElectionId", "TseCandidateSequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_FederationId",
                table: "Candidates",
                column: "FederationId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_OfficeId",
                table: "Candidates",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_PartyId",
                table: "Candidates",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_StateId",
                table: "Candidates",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_Coalitions_ElectionRoundId_OfficeId_StateId_TseCoalitionNum~",
                table: "Coalitions",
                columns: new[] { "ElectionRoundId", "OfficeId", "StateId", "TseCoalitionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coalitions_OfficeId",
                table: "Coalitions",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_Coalitions_StateId",
                table: "Coalitions",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_CandidateId",
                table: "ElectionResults",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_DatasetVersionId",
                table: "ElectionResults",
                column: "DatasetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId",
                table: "ElectionResults",
                columns: new[] { "ElectionRoundId", "SectionId", "OfficeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_ElectionRoundId_SectionId_OfficeId_VoteType~",
                table: "ElectionResults",
                columns: new[] { "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_OfficeId",
                table: "ElectionResults",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_PartyId",
                table: "ElectionResults",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_SectionId",
                table: "ElectionResults",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionRounds_ElectionId_Round",
                table: "ElectionRounds",
                columns: new[] { "ElectionId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elections_TseElectionCode",
                table: "Elections",
                column: "TseElectionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elections_Year",
                table: "Elections",
                column: "Year");

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

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralOffices_TseOfficeCode",
                table: "ElectoralOffices",
                column: "TseOfficeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralSections_MunicipalityId_ZoneId_TseSectionNumber",
                table: "ElectoralSections",
                columns: new[] { "MunicipalityId", "ZoneId", "TseSectionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralSections_ZoneId",
                table: "ElectoralSections",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralZones_MunicipalityId",
                table: "ElectoralZones",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectoralZones_StateId_TseZoneNumber",
                table: "ElectoralZones",
                columns: new[] { "StateId", "TseZoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Federations_TseFederationNumber",
                table: "Federations",
                column: "TseFederationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoliticalParties_TsePartyNumber_Acronym",
                table: "PoliticalParties",
                columns: new[] { "TsePartyNumber", "Acronym" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaces_MunicipalityId",
                table: "PollingPlaces",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaces_StateId_ZoneId_TsePollingPlaceNumber",
                table: "PollingPlaces",
                columns: new[] { "StateId", "ZoneId", "TsePollingPlaceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaces_ZoneId",
                table: "PollingPlaces",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaceSections_ElectionRoundId_SectionId",
                table: "PollingPlaceSections",
                columns: new[] { "ElectionRoundId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaceSections_PollingPlaceId",
                table: "PollingPlaceSections",
                column: "PollingPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PollingPlaceSections_SectionId",
                table: "PollingPlaceSections",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TseMunicipalityCodes_IsVerified_Confidence",
                table: "TseMunicipalityCodes",
                columns: new[] { "IsVerified", "Confidence" });

            migrationBuilder.CreateIndex(
                name: "IX_TseMunicipalityCodes_MunicipalityId",
                table: "TseMunicipalityCodes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_TseMunicipalityCodes_RawCode_ElectionYear",
                table: "TseMunicipalityCodes",
                columns: new[] { "RawCode", "ElectionYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TseMunicipalityCodes_StateId",
                table: "TseMunicipalityCodes",
                column: "StateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectionResults");

            migrationBuilder.DropTable(
                name: "ElectionStatistics");

            migrationBuilder.DropTable(
                name: "PollingPlaceSections");

            migrationBuilder.DropTable(
                name: "TseMunicipalityCodes");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "ElectoralSections");

            migrationBuilder.DropTable(
                name: "PollingPlaces");

            migrationBuilder.DropTable(
                name: "CandidateIdentities");

            migrationBuilder.DropTable(
                name: "Coalitions");

            migrationBuilder.DropTable(
                name: "Federations");

            migrationBuilder.DropTable(
                name: "PoliticalParties");

            migrationBuilder.DropTable(
                name: "ElectoralZones");

            migrationBuilder.DropTable(
                name: "ElectionRounds");

            migrationBuilder.DropTable(
                name: "ElectoralOffices");

            migrationBuilder.DropTable(
                name: "Elections");
        }
    }
}
