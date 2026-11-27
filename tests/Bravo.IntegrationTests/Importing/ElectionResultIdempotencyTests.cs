using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Repositories;
using Bravo.IntegrationTests.Configurations;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Regression cover for the unique indexes that back the import's idempotent upserts.
/// </summary>
/// <remarks>
/// PostgreSQL treats NULLs as distinct in a plain unique index. Because party votes store no
/// candidate and blank votes store neither a candidate nor a party, the previous index never
/// conflicted for them, so re-executing an import inserted a second copy and doubled the vote
/// count. The indexes now use <c>NULLS NOT DISTINCT</c>; these tests pin that behaviour.
/// </remarks>
[Collection("Database")]
public sealed class ElectionResultIdempotencyTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    private async Task<(Guid RoundId, Guid SectionId, Guid OfficeId, Guid PartyId)> SeedReferenceAsync()
    {
        var election = Election.Create(9001, 2022, 1, "ORDINARIA", "Test", null);
        await Db.ExecuteAsync(
            """
            INSERT INTO "Elections" ("Id","TseElectionCode","Year","TypeCode","TypeName","Description","CreatedAt","UpdatedAt")
            VALUES (@Id,9001,2022,1,'ORDINARIA','Test',now(),now());
            """,
            new { election.Id });

        var round = ElectionRound.Create(election.Id, 1);
        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectionRounds" ("Id","ElectionId","Round","IsRunoff","CreatedAt","UpdatedAt")
            VALUES (@Id,@ElectionId,1,false,now(),now());
            """,
            new { round.Id, round.ElectionId });

        var office = ElectoralOffice.Create(9001, "DEPUTADO FEDERAL");
        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralOffices" ("Id","TseOfficeCode","Name","CreatedAt","UpdatedAt")
            VALUES (@Id,9001,'DEPUTADO FEDERAL',now(),now());
            """,
            new { office.Id });

        var party = PoliticalParty.Create(9013, "PT", "PARTIDO DOS TRABALHADORES");
        await Db.ExecuteAsync(
            """
            INSERT INTO "PoliticalParties" ("Id","TsePartyNumber","Acronym","Name","CreatedAt","UpdatedAt")
            VALUES (@Id,9013,'PT','PARTIDO DOS TRABALHADORES',now(),now());
            """,
            new { party.Id });

        var stateId = Guid.CreateVersion7();
        await Db.ExecuteAsync(
            """
            INSERT INTO "States" ("Id","IbgeCode","Abbreviation","Name","Region","CreatedAt","UpdatedAt")
            VALUES (@Id,99001,'XT','TEST STATE','SUDESTE',now(),now());
            """,
            new { Id = stateId });

        var municipalityId = Guid.CreateVersion7();
        await Db.ExecuteAsync(
            """
            INSERT INTO "Municipalities" ("Id","IbgeCode","Name","StateId","CreatedAt","UpdatedAt")
            VALUES (@Id,99002,'TEST MUNICIPALITY',@StateId,now(),now());
            """,
            new { Id = municipalityId, StateId = stateId });

        var zone = ElectoralZone.Create(stateId, 1, municipalityId);
        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralZones" ("Id","StateId","TseZoneNumber","MunicipalityId","CreatedAt","UpdatedAt")
            VALUES (@Id,@StateId,1,@MunicipalityId,now(),now());
            """,
            new { zone.Id, StateId = stateId, MunicipalityId = municipalityId });

        var section = ElectoralSection.Create(municipalityId, zone.Id, 1);
        await Db.ExecuteAsync(
            """
            INSERT INTO "ElectoralSections" ("Id","MunicipalityId","ZoneId","TseSectionNumber","CreatedAt","UpdatedAt")
            VALUES (@Id,@MunicipalityId,@ZoneId,1,now(),now());
            """,
            new { section.Id, MunicipalityId = municipalityId, ZoneId = zone.Id });

        return (round.Id, section.Id, office.Id, party.Id);
    }

    private async Task<(Guid DatasetId, Guid VersionId)> SeedVersionAsync()
    {
        var datasetId = Guid.CreateVersion7();
        await Db.ExecuteAsync(
            """
            INSERT INTO "ImportDatasets"
                ("Id","Provider","Dataset","ElectionYear","LayoutVersion","SourceUrl",
                 "SourceFileName","Checksum","SizeBytes","AcquiredAt","CreatedAt","UpdatedAt")
            VALUES (@Id,'TSE','votacao_secao',2022,'v1','url','file','checksum',1,now(),now(),now());
            """,
            new { Id = datasetId });

        var versionId = Guid.CreateVersion7();
        await Db.ExecuteAsync(
            """
            INSERT INTO "DatasetVersions"
                ("Id","ImportDatasetId","Scope","Status","IsActive","CreatedAt","UpdatedAt")
            VALUES (@Id,@ImportDatasetId,'test-scope','Completed',true,now(),now());
            """,
            new { Id = versionId, ImportDatasetId = datasetId });

        return (datasetId, versionId);
    }

    private async Task<int> CountAsync(Guid versionId)
    {
        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "ElectionResults" WHERE "DatasetVersionId" = @id;""";
        command.Parameters.AddWithValue("id", versionId);

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task PartyVotesAreNotDuplicatedWhenTheSameBatchIsWrittenTwice()
    {
        var (roundId, sectionId, officeId, partyId) = await SeedReferenceAsync();
        var (_, versionId) = await SeedVersionAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IElectionResultRepository>();

        // A party vote has PartyId set and CandidateId null, which is exactly the row shape the
        // old index could not deduplicate.
        ElectionResult Build() => ElectionResult.Create(
            roundId, sectionId, officeId, VoteType.Party, null, partyId, 13, 7, versionId);

        await repository.UpsertResultsAsync([Build()]);
        await repository.UpsertResultsAsync([Build()]);

        (await CountAsync(versionId)).Should().Be(1);
    }

    [Fact]
    public async Task BlankVotesAreNotDuplicatedWhenTheSameBatchIsWrittenTwice()
    {
        var (roundId, sectionId, officeId, _) = await SeedReferenceAsync();
        var (_, versionId) = await SeedVersionAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IElectionResultRepository>();

        // Blank votes store neither a candidate nor a party.
        ElectionResult Build() => ElectionResult.Create(
            roundId, sectionId, officeId, VoteType.Blank, null, null, 95, 3, versionId);

        await repository.UpsertResultsAsync([Build()]);
        await repository.UpsertResultsAsync([Build()]);

        (await CountAsync(versionId)).Should().Be(1);
    }

    [Fact]
    public async Task RepeatingTheWholeBatchKeepsVoteTotalsStable()
    {
        var (roundId, sectionId, officeId, partyId) = await SeedReferenceAsync();
        var (_, versionId) = await SeedVersionAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IElectionResultRepository>();

        var batch = new[]
        {
            ElectionResult.Create(roundId, sectionId, officeId, VoteType.Party, null, partyId, 13, 7, versionId),
            ElectionResult.Create(roundId, sectionId, officeId, VoteType.Blank, null, null, 95, 3, versionId),
            ElectionResult.Create(roundId, sectionId, officeId, VoteType.Null, null, null, 96, 2, versionId)
        };

        for (var run = 0; run < 3; run++)
            await repository.UpsertResultsAsync(batch);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM("Votes"), 0)::bigint FROM "ElectionResults" WHERE "DatasetVersionId" = @id;
            """;
        command.Parameters.AddWithValue("id", versionId);

        ((long)(await command.ExecuteScalarAsync())!).Should().Be(12);
    }

    [Fact]
    public async Task IdentitiesWithoutBirthDateAreNotDuplicated()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICandidateRepository>();

        // TSE has candidates with neither CPF nor birth date. PostgreSQL would accept unlimited
        // duplicates for them without NULLS NOT DISTINCT on the filtered unique index.
        static CandidateIdentity Build() =>
            CandidateIdentity.Create(null, "NO CPF NO BIRTH DATE", null);

        await repository.EnsureIdentitiesAsync([Build()]);
        await repository.EnsureIdentitiesAsync([Build()]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM "CandidateIdentities" WHERE "FullName" = 'NO CPF NO BIRTH DATE';
            """;
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(1);
    }
}
