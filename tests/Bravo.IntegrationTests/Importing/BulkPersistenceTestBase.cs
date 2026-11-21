using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Repositories;
using Bravo.IntegrationTests.Configurations;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Shared reference rows for the bulk upsert suites.
/// </summary>
/// <remarks>
/// The migrated repositories are exercised against a real PostgreSQL instance because the
/// behaviour under test is exactly what only a database can prove: unique-index conflict
/// resolution, <c>NULLS NOT DISTINCT</c>, foreign keys, check constraints, binary type mapping
/// and whether a no-op upsert produced a new row version.
/// </remarks>
[Collection("Database")]
public abstract class BulkPersistenceTestBase(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    protected T Repository<T>(IServiceProvider services) where T : notnull
        => services.GetRequiredService<T>();

    protected async Task<ReferenceSeed> SeedReferenceAsync(string suffix = "A")
    {
        var election = Election.Create(9100 + suffix[0], 2022, 1, "ORDINARIA", "Test", null);

        await Db.ExecuteAsync(
            """
            INSERT INTO "Elections" ("Id","TseElectionCode","Year","TypeCode","TypeName","Description","CreatedAt","UpdatedAt")
            VALUES (@Id,9100,2022,1,'ORDINARIA','Test',now(),now())
            ON CONFLICT ("TseElectionCode") DO NOTHING;
            """,
            new { election.Id });

        var electionId = await Db.ExecuteScalarAsync<Guid>(
            """SELECT "Id" FROM "Elections" WHERE "TseElectionCode" = 9100;""");

        var round = ElectionRound.Create(electionId, 1, "1o turno");

        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectionRounds" ("Id","ElectionId","Round","Name","IsRunoff","CreatedAt","UpdatedAt")
            VALUES (@Id,@ElectionId,1,'1o turno',false,now(),now())
            ON CONFLICT ("ElectionId","Round") DO NOTHING;
            """,
            new { round.Id, ElectionId = electionId });

        var roundId = await Db.ExecuteScalarAsync<Guid>(
            """SELECT "Id" FROM "ElectionRounds" WHERE "ElectionId" = @ElectionId AND "Round" = 1;""",
            new { ElectionId = electionId });

        var office = ElectoralOffice.Create(9100, "DEPUTADO FEDERAL");

        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralOffices" ("Id","TseOfficeCode","Name","CreatedAt","UpdatedAt")
            VALUES (@Id,9100,'DEPUTADO FEDERAL',now(),now())
            ON CONFLICT ("TseOfficeCode") DO NOTHING;
            """,
            new { office.Id });

        var officeId = await Db.ExecuteScalarAsync<Guid>(
            """SELECT "Id" FROM "ElectoralOffices" WHERE "TseOfficeCode" = 9100;""");

        var party = PoliticalParty.Create(9013, "PT", "PARTIDO DOS TRABALHADORES");

        await Db.ExecuteAsync(
            """
            INSERT INTO "PoliticalParties" ("Id","TsePartyNumber","Acronym","Name","CreatedAt","UpdatedAt")
            VALUES (@Id,9013,'PT','PARTIDO DOS TRABALHADORES',now(),now())
            ON CONFLICT ("TsePartyNumber","Acronym") DO NOTHING;
            """,
            new { party.Id });

        var partyId = await Db.ExecuteScalarAsync<Guid>(
            """SELECT "Id" FROM "PoliticalParties" WHERE "TsePartyNumber" = 9013;""");

        var stateId = Guid.CreateVersion7();

        await Db.ExecuteAsync(
            """
            INSERT INTO "States" ("Id","IbgeCode","Abbreviation","Name","Region","CreatedAt","UpdatedAt")
            VALUES (@Id,91001,'XT','TEST STATE','SUDESTE',now(),now());
            """,
            new { Id = stateId });

        var municipalityId = Guid.CreateVersion7();

        await Db.ExecuteAsync(
            """
            INSERT INTO "Municipalities" ("Id","IbgeCode","Name","StateId","CreatedAt","UpdatedAt")
            VALUES (@Id,91002,'TEST MUNICIPALITY',@StateId,now(),now());
            """,
            new { Id = municipalityId, StateId = stateId });

        var zone = ElectoralZone.Create(stateId, 91, municipalityId);

        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralZones" ("Id","StateId","TseZoneNumber","MunicipalityId","CreatedAt","UpdatedAt")
            VALUES (@Id,@StateId,91,@MunicipalityId,now(),now());
            """,
            new { zone.Id, StateId = stateId, MunicipalityId = municipalityId });

        var section = ElectoralSection.Create(municipalityId, zone.Id, 9101);

        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralSections" ("Id","MunicipalityId","ZoneId","TseSectionNumber","CreatedAt","UpdatedAt")
            VALUES (@Id,@MunicipalityId,@ZoneId,9101,now(),now());
            """,
            new { section.Id, MunicipalityId = municipalityId, ZoneId = zone.Id });

        var datasetId = Guid.CreateVersion7();

        await Db.ExecuteAsync(
            """
            INSERT INTO "ImportDatasets"
                ("Id","Provider","Dataset","ElectionYear","LayoutVersion","SourceUrl",
                 "SourceFileName","Checksum","SizeBytes","AcquiredAt","CreatedAt","UpdatedAt")
            VALUES (@Id,'TSE','votacao_secao',2022,'v1','url','file',@Checksum,1,now(),now(),now());
            """,
            new { Id = datasetId, Checksum = Guid.NewGuid().ToString("N") });

        var versionId = Guid.CreateVersion7();

        await Db.ExecuteAsync(
            """
            INSERT INTO "DatasetVersions"
                ("Id","ImportDatasetId","Scope","Status","IsActive","CreatedAt","UpdatedAt")
            VALUES (@Id,@ImportDatasetId,@Scope,'Completed',true,now(),now());
            """,
            new { Id = versionId, ImportDatasetId = datasetId, Scope = UniqueScope("version") });

        return new ReferenceSeed(
            electionId, roundId, officeId, partyId, stateId, municipalityId,
            zone.Id, section.Id, versionId);
    }

    /// <summary>
    /// PostgreSQL exposes the row version as <c>xmin</c>. If a no-op upsert really did skip the
    /// write, the value is unchanged, which no amount of application-level bookkeeping can fake.
    /// </summary>
    protected async Task<string> RowVersionAsync(string table, Guid id)
    {
        await using var command = Db.CreateCommand();
        command.CommandText = $"SELECT xmin::text FROM \"{table}\" WHERE \"Id\" = @id;";
        command.Parameters.AddWithValue("id", id);

        return (string)(await command.ExecuteScalarAsync())!;
    }

    protected async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
        => await Db.ExecuteScalarAsync<T>(sql, parameters);

    protected async Task<int> CountAsync(string table, string where, object parameters)
        => await Db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM \"{table}\" WHERE {where};", parameters);

    protected sealed record ReferenceSeed(
        Guid ElectionId,
        Guid RoundId,
        Guid OfficeId,
        Guid PartyId,
        Guid StateId,
        Guid MunicipalityId,
        Guid ZoneId,
        Guid SectionId,
        Guid VersionId);
}
