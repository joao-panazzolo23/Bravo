using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Dapper;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Binary COPY coverage for electoral zones, sections, polling places and assignments.
/// </summary>
[Collection("Database")]
public sealed class ElectoralStructureBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    [Fact]
    public async Task ZonesInsertAndFillMunicipalityOnlyOnce()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var zone = ElectoralZone.Create(seed.StateId, 900, seed.MunicipalityId);
        await repository.UpsertZonesAsync([zone]);

        (await ScalarAsync<Guid?>(
            """SELECT "MunicipalityId" FROM "ElectoralZones" WHERE "Id" = @id;""", new { id = zone.Id }))
            .Should().Be(seed.MunicipalityId);

        // A later batch without a municipality must not erase the known one; the guard then
        // skips the write because the effective value is unchanged.
        var before = await RowVersionAsync("ElectoralZones", zone.Id);
        await repository.UpsertZonesAsync([ElectoralZone.Create(seed.StateId, 900, null)]);

        (await ScalarAsync<Guid?>(
            """SELECT "MunicipalityId" FROM "ElectoralZones" WHERE "Id" = @id;""", new { id = zone.Id }))
            .Should().Be(seed.MunicipalityId);

        (await RowVersionAsync("ElectoralZones", zone.Id)).Should().Be(before);
    }

    [Fact]
    public async Task ZonesAreUpdatedWhenTheMunicipalityBecomesKnown()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var zone = ElectoralZone.Create(seed.StateId, 901, null);
        await repository.UpsertZonesAsync([zone]);

        var secondMunicipality = Guid.CreateVersion7();
        await Db.ExecuteAsync(
            """
            INSERT INTO "Municipalities" ("Id","IbgeCode","Name","StateId","CreatedAt","UpdatedAt")
            VALUES (@Id,91003,'SECOND',@StateId,now(),now());
            """,
            new { Id = secondMunicipality, StateId = seed.StateId });

        var before = await RowVersionAsync("ElectoralZones", zone.Id);
        await repository.UpsertZonesAsync([ElectoralZone.Create(seed.StateId, 901, secondMunicipality)]);

        (await ScalarAsync<Guid?>(
            """SELECT "MunicipalityId" FROM "ElectoralZones" WHERE "Id" = @id;""", new { id = zone.Id }))
            .Should().Be(secondMunicipality);

        (await RowVersionAsync("ElectoralZones", zone.Id)).Should().NotBe(before);
    }

    [Fact]
    public async Task DuplicateZoneKeysInOneBatchCollapse()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        await repository.UpsertZonesAsync([
            ElectoralZone.Create(seed.StateId, 902, null),
            ElectoralZone.Create(seed.StateId, 902, seed.MunicipalityId)
        ]);

        (await CountAsync("ElectoralZones", "\"StateId\" = @state AND \"TseZoneNumber\" = 902",
            new { state = seed.StateId })).Should().Be(1);

        (await ScalarAsync<Guid?>(
            """SELECT "MunicipalityId" FROM "ElectoralZones" WHERE "StateId" = @state AND "TseZoneNumber" = 902;""",
            new { state = seed.StateId })).Should().Be(seed.MunicipalityId);
    }

    [Fact]
    public async Task SectionsInsertAndReimportKeepsTheStoredIdentity()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var section = ElectoralSection.Create(seed.MunicipalityId, seed.ZoneId, 9901);
        await repository.UpsertSectionsAsync([section]);

        var before = await RowVersionAsync("ElectoralSections", section.Id);

        await repository.UpsertSectionsAsync([ElectoralSection.Create(seed.MunicipalityId, seed.ZoneId, 9901)]);

        // Sections have no mutable business column and nothing reads their UpdatedAt as a
        // heartbeat, so a repeated key must not produce a write at all.
        (await CountAsync("ElectoralSections", "\"TseSectionNumber\" = 9901", new { })).Should().Be(1);
        (await ScalarAsync<Guid>(
            """SELECT "Id" FROM "ElectoralSections" WHERE "TseSectionNumber" = 9901;"""))
            .Should().Be(section.Id);
        (await RowVersionAsync("ElectoralSections", section.Id)).Should().Be(before);
    }

    [Fact]
    public async Task DuplicateSectionKeysInOneBatchCollapse()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        await repository.UpsertSectionsAsync([
            ElectoralSection.Create(seed.MunicipalityId, seed.ZoneId, 9902),
            ElectoralSection.Create(seed.MunicipalityId, seed.ZoneId, 9902)
        ]);

        (await CountAsync("ElectoralSections", "\"TseSectionNumber\" = 9902", new { })).Should().Be(1);
    }

    [Fact]
    public async Task PollingPlacesRoundTripNullableColumnsAndPrecision()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var place = PollingPlace.Create(
            seed.StateId, seed.MunicipalityId, seed.ZoneId, 9001, "ESCOLA MUNICIPAL",
            "RUA A, 100", "CENTRO", null, -23.5505201m, -46.6333082m);

        await repository.UpsertPollingPlacesAsync([place]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "Name", "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude",
                   "MunicipalityId"
            FROM "PollingPlaces" WHERE "Id" = @id;
            """;
        command.Parameters.AddWithValue("id", place.Id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetString(0).Should().Be("ESCOLA MUNICIPAL");
        reader.GetString(1).Should().Be("RUA A, 100");
        reader.GetString(2).Should().Be("CENTRO");
        reader.IsDBNull(3).Should().BeTrue();
        reader.GetDecimal(4).Should().Be(-23.5505201m);
        reader.GetDecimal(5).Should().Be(-46.6333082m);
        reader.GetGuid(6).Should().Be(seed.MunicipalityId);
    }

    [Fact]
    public async Task UnchangedPollingPlaceIsNotRewrittenButAChangedOneIs()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var place = PollingPlace.Create(
            seed.StateId, seed.MunicipalityId, seed.ZoneId, 9002, "PLACE",
            null, null, null, null, null);

        await repository.UpsertPollingPlacesAsync([place]);
        var before = await RowVersionAsync("PollingPlaces", place.Id);

        await repository.UpsertPollingPlacesAsync([
            PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9002, "PLACE",
                null, null, null, null, null)
        ]);

        (await RowVersionAsync("PollingPlaces", place.Id)).Should().Be(before);

        await repository.UpsertPollingPlacesAsync([
            PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9002, "RENAMED",
                "NEW ADDRESS", null, null, null, null)
        ]);

        (await RowVersionAsync("PollingPlaces", place.Id)).Should().NotBe(before);
        (await ScalarAsync<string>(
            """SELECT "Name" FROM "PollingPlaces" WHERE "Id" = @id;""", new { id = place.Id }))
            .Should().Be("RENAMED");
    }

    [Fact]
    public async Task AssignmentsInsertAndUpdateThePlaceInPlace()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var first = PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9003, "FIRST", null, null, null, null, null);
        var second = PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9004, "SECOND", null, null, null, null, null);
        await repository.UpsertPollingPlacesAsync([first, second]);

        var assignment = PollingPlaceSection.Create(seed.RoundId, first.Id, seed.SectionId);
        await repository.UpsertAssignmentsAsync([assignment]);

        (await ScalarAsync<Guid>(
            """SELECT "PollingPlaceId" FROM "PollingPlaceSections" WHERE "Id" = @id;""",
            new { id = assignment.Id })).Should().Be(first.Id);

        var before = await RowVersionAsync("PollingPlaceSections", assignment.Id);

        await repository.UpsertAssignmentsAsync([
            PollingPlaceSection.Create(seed.RoundId, second.Id, seed.SectionId)
        ]);

        (await ScalarAsync<Guid>(
            """SELECT "PollingPlaceId" FROM "PollingPlaceSections" WHERE "ElectionRoundId" = @round AND "SectionId" = @section;""",
            new { round = seed.RoundId, section = seed.SectionId })).Should().Be(second.Id);

        (await RowVersionAsync("PollingPlaceSections", assignment.Id)).Should().NotBe(before);
    }

    [Fact]
    public async Task UnchangedAssignmentIsNotRewritten()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var place = PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9005, "PLACE", null, null, null, null, null);
        await repository.UpsertPollingPlacesAsync([place]);

        var assignment = PollingPlaceSection.Create(seed.RoundId, place.Id, seed.SectionId);
        await repository.UpsertAssignmentsAsync([assignment]);
        var before = await RowVersionAsync("PollingPlaceSections", assignment.Id);

        await repository.UpsertAssignmentsAsync([
            PollingPlaceSection.Create(seed.RoundId, place.Id, seed.SectionId)
        ]);

        (await RowVersionAsync("PollingPlaceSections", assignment.Id)).Should().Be(before);
    }

    [Fact]
    public async Task DuplicateAssignmentKeysInOneBatchCollapse()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        var place = PollingPlace.Create(seed.StateId, seed.MunicipalityId, seed.ZoneId, 9006, "PLACE", null, null, null, null, null);
        await repository.UpsertPollingPlacesAsync([place]);

        await repository.UpsertAssignmentsAsync([
            PollingPlaceSection.Create(seed.RoundId, place.Id, seed.SectionId),
            PollingPlaceSection.Create(seed.RoundId, place.Id, seed.SectionId)
        ]);

        (await CountAsync("PollingPlaceSections",
            "\"ElectionRoundId\" = @round AND \"SectionId\" = @section",
            new { round = seed.RoundId, section = seed.SectionId })).Should().Be(1);
    }

    [Fact]
    public async Task EmptyBatchesAreNoOps()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        (await repository.UpsertZonesAsync([])).Should().Be(0);
        (await repository.UpsertSectionsAsync([])).Should().Be(0);
        (await repository.UpsertPollingPlacesAsync([])).Should().Be(0);
        (await repository.UpsertAssignmentsAsync([])).Should().Be(0);
    }

    [Fact]
    public async Task CancellationRollsBackAndDropsStaging()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectoralStructureRepository>(scope.ServiceProvider);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await repository.UpsertSectionsAsync(
            [ElectoralSection.Create(seed.MunicipalityId, seed.ZoneId, 9903)],
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        (await CountAsync("ElectoralSections", "\"TseSectionNumber\" = 9903", new { })).Should().Be(0);
    }

    [Fact]
    public async Task CancellationOfAnIdentityBatchLeavesNoStagingRows()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await repository.EnsureIdentitiesAsync(
            [CandidateIdentity.Create("00011122233", "CANCELLED", null)],
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        (await CountAsync("CandidateIdentities", "\"Cpf\" = '00011122233'", new { })).Should().Be(0);
    }
}
