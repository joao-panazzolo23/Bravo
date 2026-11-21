using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Geography.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Binary COPY coverage for coalitions, TSE municipality codes, states, municipalities and dead
/// letters.
/// </summary>
[Collection("Database")]
public sealed class ReferenceBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    [Fact]
    public async Task CoalitionsInsertUpdateAndSkipNoOpWrites()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICoalitionRepository>(scope.ServiceProvider);

        var coalition = Coalition.Create(
            seed.RoundId, seed.OfficeId, seed.StateId, 7001, "COLIGAÇÃO A", "PT/PCdoB");

        await repository.UpsertAsync([coalition]);

        var before = await RowVersionAsync("Coalitions", coalition.Id);

        await repository.UpsertAsync([
            Coalition.Create(seed.RoundId, seed.OfficeId, seed.StateId, 7001, "COLIGAÇÃO A", "PT/PCdoB")
        ]);

        (await RowVersionAsync("Coalitions", coalition.Id)).Should().Be(before);

        await repository.UpsertAsync([
            Coalition.Create(seed.RoundId, seed.OfficeId, seed.StateId, 7001, "COLIGAÇÃO B", null)
        ]);

        (await ScalarAsync<string>(
            """SELECT "Name" FROM "Coalitions" WHERE "Id" = @id;""", new { id = coalition.Id }))
            .Should().Be("COLIGAÇÃO B");

        // Composition is nullable and must survive the binary null write.
        (await ScalarAsync<string?>(
            """SELECT "Composition" FROM "Coalitions" WHERE "Id" = @id;""", new { id = coalition.Id }))
            .Should().BeNull();
    }

    [Fact]
    public async Task CoalitionBigintNumberSurvivesTheBinaryRoundTrip()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICoalitionRepository>(scope.ServiceProvider);

        const long bigNumber = 9_000_000_000;

        await repository.UpsertAsync([
            Coalition.Create(seed.RoundId, seed.OfficeId, seed.StateId, bigNumber, "BIG", null)
        ]);

        (await ScalarAsync<long>(
            """SELECT "TseCoalitionNumber" FROM "Coalitions" WHERE "Name" = 'BIG';"""))
            .Should().Be(bigNumber);

        var ids = await repository.GetIdsAsync(seed.StateId);
        ids.Should().ContainKey((seed.RoundId, seed.OfficeId, bigNumber));
    }

    [Fact]
    public async Task MunicipalityCodesRoundTripEnumAsTextAndDecimalPrecision()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ITseMunicipalityCodeRepository>(scope.ServiceProvider);

        var code = TseMunicipalityCode.Create(
            "91002", seed.StateId, seed.MunicipalityId, 2022,
            MunicipalityMatchMethod.NormalizedName, 0.60m);

        await repository.UpsertAsync([code]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "MatchMethod", "Confidence", "IsVerified", "VerifiedAt"
            FROM "TseMunicipalityCodes" WHERE "Id" = @id;
            """;
        command.Parameters.AddWithValue("id", code.Id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetString(0).Should().Be("NormalizedName");
        reader.GetDecimal(1).Should().Be(0.60m);
        reader.GetBoolean(2).Should().BeFalse();
        reader.IsDBNull(3).Should().BeTrue();
    }

    [Fact]
    public async Task MunicipalityCodeKeepsAnEarlierResolution()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ITseMunicipalityCodeRepository>(scope.ServiceProvider);

        var code = TseMunicipalityCode.Create(
            "91999", seed.StateId, seed.MunicipalityId, 2022,
            MunicipalityMatchMethod.NormalizedName, 0.60m);

        await repository.UpsertAsync([code]);

        // A later unresolved pass must not erase the known municipality.
        await repository.UpsertAsync([
            TseMunicipalityCode.Create(
                "91999", seed.StateId, null, 2022, MunicipalityMatchMethod.Unresolved, 0m)
        ]);

        (await ScalarAsync<Guid?>(
            """SELECT "MunicipalityId" FROM "TseMunicipalityCodes" WHERE "RawCode" = '91999';"""))
            .Should().Be(seed.MunicipalityId);

        (await ScalarAsync<string>(
            """SELECT "MatchMethod" FROM "TseMunicipalityCodes" WHERE "RawCode" = '91999';"""))
            .Should().Be("Unresolved");
    }

    [Fact]
    public async Task StatesRoundTripAndSkipNoOpWrites()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IGeographyRepository>(scope.ServiceProvider);

        var state = State.Create(92001, "XZ", "TEST STATE", "NORDESTE");
        await repository.UpsertStatesAsync([state]);

        var before = await RowVersionAsync("States", state.Id);

        await repository.UpsertStatesAsync([State.Create(92001, "XZ", "TEST STATE", "NORDESTE")]);

        (await RowVersionAsync("States", state.Id)).Should().Be(before);

        await repository.UpsertStatesAsync([State.Create(92001, "XZ", "RENAMED STATE", "NORDESTE")]);

        (await RowVersionAsync("States", state.Id)).Should().NotBe(before);
        (await ScalarAsync<string>(
            """SELECT "Name" FROM "States" WHERE "Abbreviation" = 'XZ';"""))
            .Should().Be("RENAMED STATE");
    }

    [Fact]
    public async Task MunicipalitiesResolveTheirStateAndReportSkippedOnes()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var geography = Repository<IGeographyRepository>(scope.ServiceProvider);

        await geography.UpsertStatesAsync([State.Create(93001, "XY", "MUNI STATE", "SUL")]);

        var persisted = await geography.UpsertMunicipalitiesAsync([
            new MunicipalityUpsert(93002, "MUNI ONE", 93001),
            new MunicipalityUpsert(93003, "MUNI TWO", 93001)
        ]);

        persisted.Should().Be(2);

        var stateId = await ScalarAsync<Guid>(
            """SELECT "Id" FROM "States" WHERE "IbgeCode" = 93001;""");

        (await ScalarAsync<Guid>(
            """SELECT "StateId" FROM "Municipalities" WHERE "IbgeCode" = 93002;"""))
            .Should().Be(stateId);

        // A municipality whose state is unknown must be skipped and reported, which is what the
        // IBGE source turns into a permanent failure instead of a dangling reference.
        var partial = await geography.UpsertMunicipalitiesAsync([
            new MunicipalityUpsert(93004, "MUNI THREE", 93001),
            new MunicipalityUpsert(93005, "ORPHAN", 99999)
        ]);

        partial.Should().Be(1);

        (await CountAsync("Municipalities", "\"IbgeCode\" = 93005", new { })).Should().Be(0);
    }

    [Fact]
    public async Task MunicipalityUpdateChangesNameWithoutChangingTheKey()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var geography = Repository<IGeographyRepository>(scope.ServiceProvider);

        await geography.UpsertStatesAsync([State.Create(94001, "XW", "STATE", "CENTRO-OESTE")]);
        await geography.UpsertMunicipalitiesAsync([new MunicipalityUpsert(94002, "OLD NAME", 94001)]);

        var id = await ScalarAsync<Guid>(
            """SELECT "Id" FROM "Municipalities" WHERE "IbgeCode" = 94002;""");

        var before = await RowVersionAsync("Municipalities", id);

        await geography.UpsertMunicipalitiesAsync([new MunicipalityUpsert(94002, "NEW NAME", 94001)]);

        (await ScalarAsync<string>(
            """SELECT "Name" FROM "Municipalities" WHERE "IbgeCode" = 94002;"""))
            .Should().Be("NEW NAME");

        // A new identifier is staged on every call, but the conflict preserves the stored row.
        (await ScalarAsync<Guid>(
            """SELECT "Id" FROM "Municipalities" WHERE "IbgeCode" = 94002;"""))
            .Should().Be(id);

        (await RowVersionAsync("Municipalities", id)).Should().NotBe(before);
    }
}
