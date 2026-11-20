using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Geography.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Covers the state-scoped structure reads that replaced nationwide scans, and the bulk
/// identity upsert that replaced one roundtrip per candidate.
/// </summary>
[Collection("Database")]
public sealed class ScopedElectoralStructureTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    private async Task<(Guid StateId, Guid MunicipalityId)> SeedStateAsync(string acronym)
    {
        await using var command = Db.CreateCommand();
        command.CommandText = """
            INSERT INTO "States" ("Id","IbgeCode","Abbreviation","Name","Region","CreatedAt","UpdatedAt")
            VALUES (@id, @ibge, @acronym, @name, 'SUDESTE', now(), now());

            INSERT INTO "Municipalities" ("Id","IbgeCode","Name","StateId","CreatedAt","UpdatedAt")
            VALUES (@muniId, @muniIbge, @muniName, @id, now(), now());
            """;
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("ibge", Random.Shared.Next(10_000_000, 99_999_999));
        command.Parameters.AddWithValue("acronym", acronym);
        command.Parameters.AddWithValue("name", $"STATE {acronym}");
        var muniId = Guid.CreateVersion7();
        command.Parameters.AddWithValue("muniId", muniId);
        command.Parameters.AddWithValue("muniIbge", Random.Shared.Next(10_000_000, 99_999_999));
        command.Parameters.AddWithValue("muniName", $"MUNICIPALITY {acronym}");
        await command.ExecuteNonQueryAsync();

        await using var read = Db.CreateCommand();
        read.CommandText = """SELECT "Id" FROM "States" WHERE "Abbreviation" = @acronym;""";
        read.Parameters.AddWithValue("acronym", acronym);
        var stateId = (Guid)(await read.ExecuteScalarAsync())!;

        return (stateId, muniId);
    }

    [Fact]
    public async Task ZoneReadsOnlyReturnTheRequestedState()
    {
        var (stateA, _) = await SeedStateAsync("XA");
        var (stateB, _) = await SeedStateAsync("XB");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var structure = scope.ServiceProvider.GetRequiredService<IElectoralStructureRepository>();

        var zoneA = ElectoralZone.Create(stateA, 1, null);
        var zoneB = ElectoralZone.Create(stateB, 1, null);

        await structure.UpsertZonesAsync([zoneA, zoneB]);

        var result = await structure.GetZoneIdsAsync(stateA);

        result.Should().ContainKey((stateA, 1));
        result.Should().NotContainKey((stateB, 1));
        result.Values.Should().OnlyContain(id => id == zoneA.Id);
    }

    [Fact]
    public async Task SectionReadsOnlyReturnTheRequestedState()
    {
        var (stateA, muniA) = await SeedStateAsync("XC");
        var (stateB, muniB) = await SeedStateAsync("XD");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var structure = scope.ServiceProvider.GetRequiredService<IElectoralStructureRepository>();

        var zoneA = ElectoralZone.Create(stateA, 10, muniA);
        var zoneB = ElectoralZone.Create(stateB, 10, muniB);

        await structure.UpsertZonesAsync([zoneA, zoneB]);

        var sectionA = ElectoralSection.Create(muniA, zoneA.Id, 1);
        var sectionB = ElectoralSection.Create(muniB, zoneB.Id, 1);

        await structure.UpsertSectionsAsync([sectionA, sectionB]);

        // Same municipality/zone/section numbers in both states: only the requested state's
        // rows may come back, or two states' votes would collapse onto one section.
        var result = await structure.GetSectionIdsAsync(stateA);

        result.Should().ContainKey((muniA, zoneA.Id, 1));
        result.Should().NotContainKey((muniB, zoneB.Id, 1));
        result.Values.Should().OnlyContain(id => id == sectionA.Id);
    }

    [Fact]
    public async Task PollingPlaceReadsOnlyReturnTheRequestedState()
    {
        var (stateA, muniA) = await SeedStateAsync("XE");
        var (stateB, muniB) = await SeedStateAsync("XF");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var structure = scope.ServiceProvider.GetRequiredService<IElectoralStructureRepository>();

        var zoneA = ElectoralZone.Create(stateA, 5, muniA);
        var zoneB = ElectoralZone.Create(stateB, 5, muniB);

        await structure.UpsertZonesAsync([zoneA, zoneB]);

        var placeA = PollingPlace.Create(stateA, muniA, zoneA.Id, 1, "A", null, null, null, null, null);
        var placeB = PollingPlace.Create(stateB, muniB, zoneB.Id, 1, "B", null, null, null, null, null);

        await structure.UpsertPollingPlacesAsync([placeA, placeB]);

        var result = await structure.GetPollingPlaceIdsAsync(stateA);

        result.Should().ContainKey((stateA, zoneA.Id, 1));
        result.Should().NotContainKey((stateB, zoneB.Id, 1));
        result.Values.Should().OnlyContain(id => id == placeA.Id);
    }

    [Fact]
    public async Task MunicipalityReadsOnlyReturnTheRequestedState()
    {
        var (stateA, muniA) = await SeedStateAsync("XG");
        var (stateB, muniB) = await SeedStateAsync("XH");

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var geography = scope.ServiceProvider.GetRequiredService<IGeographyRepository>();

        var result = await geography.GetMunicipalitiesByStateAsync(stateA);

        result.Should().Contain(municipality => municipality.Id == muniA);
        result.Should().NotContain(municipality => municipality.Id == muniB);
        result.Should().OnlyContain(municipality => municipality.StateId == stateA);
    }

    [Fact]
    public async Task BulkIdentityUpsertIsIdempotentAndReturnsEveryId()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var candidates = scope.ServiceProvider.GetRequiredService<ICandidateRepository>();

        var identities = new[]
        {
            CandidateIdentity.Create("12345678901", "PERSON WITH CPF", new DateOnly(1980, 1, 1)),
            CandidateIdentity.Create(null, "PERSON WITHOUT CPF", new DateOnly(1990, 2, 2))
        };

        var first = await candidates.UpsertIdentitiesAsync(identities);
        var second = await candidates.UpsertIdentitiesAsync(identities);

        first.Should().HaveCount(2);
        second.Should().HaveCount(2);

        // Re-upserting must converge on the same rows rather than create duplicates.
        first.Should().Equal(second);
    }

    [Fact]
    public async Task InvalidCpfIsStoredAndKeyedAsAnIdentityWithoutCpf()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var candidates = scope.ServiceProvider.GetRequiredService<ICandidateRepository>();

        // TSE emits "-4" for withheld CPFs. It is not eleven digits, so the entity discards it
        // and the row is unique by (name, birth date). The returned key must reflect that.
        var identity = CandidateIdentity.Create("-4", "WITHHELD CPF", new DateOnly(1970, 3, 3));

        var persisted = await candidates.UpsertIdentitiesAsync([identity]);

        var key = CandidateIdentityKey.Of("-4", "WITHHELD CPF", new DateOnly(1970, 3, 3));

        persisted.Should().ContainKey(key);
        key.Should().StartWith("name:");

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM "CandidateIdentities"
            WHERE "FullName" = 'WITHHELD CPF' AND "BirthDate" = DATE '1970-03-03' AND "Cpf" IS NULL;
            """;
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentIdentityUpsertsConvergeOnOneRowPerPerson()
    {
        // Two year-level imports racing for the same people. Each creates its own entity
        // instances, so the ids differ but the natural keys collide; the database must
        // converge on one row and both callers must receive the same stored id.
        const int people = 20;

        static CandidateIdentity[] Build(int offset) => Enumerable.Range(0, people)
            .Select(index => CandidateIdentity.Create(
                $"1234567{index:D4}", $"CONCURRENT PERSON {index}", new DateOnly(1980, 1, 1)))
            .ToArray();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICandidateRepository>();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                repository.UpsertIdentitiesAsync(Build(0)))));

        // Every caller must observe the same winning id per person.
        var ids = results.SelectMany(result => result.Values).Distinct().ToArray();

        ids.Should().HaveCount(people);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM "CandidateIdentities" WHERE "FullName" LIKE 'CONCURRENT PERSON %';
            """;
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(people);
    }
}
