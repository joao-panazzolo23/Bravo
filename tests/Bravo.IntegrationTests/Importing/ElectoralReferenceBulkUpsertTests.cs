using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Covers the binary-COPY reference upserts. These assert the persistence semantics the JSONB
/// implementation provided, since the storage mechanism changed underneath them.
/// </summary>
[Collection("Database")]
public sealed class ElectoralReferenceBulkUpsertTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    private IElectoralReferenceRepository Repository(IServiceProvider services)
        => services.GetRequiredService<IElectoralReferenceRepository>();

    [Fact]
    public async Task OfficesAreInsertedAndUpdatedByIdempotently()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        await repository.UpsertOfficesAsync([
            ElectoralOffice.Create(1, "Presidente"),
            ElectoralOffice.Create(2, "Governador")
        ]);

        await repository.UpsertOfficesAsync([
            ElectoralOffice.Create(1, "Presidente da República"),
            ElectoralOffice.Create(2, "Governador")
        ]);

        var ids = await repository.GetOfficeIdsAsync();

        ids.Should().HaveCount(2);

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT "Name" FROM "ElectoralOffices" WHERE "TseOfficeCode" = 1;""";
        (await command.ExecuteScalarAsync()).Should().Be("Presidente da República");
    }

    [Fact]
    public async Task DuplicateKeysInOneBatchDoNotFailTheStatement()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        // The same office code twice in one batch previously required care: PostgreSQL rejects
        // an upsert that affects one row twice. Last value wins, matching staging order.
        await repository.UpsertOfficesAsync([
            ElectoralOffice.Create(7, "First"),
            ElectoralOffice.Create(7, "Second")
        ]);

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "ElectoralOffices" WHERE "TseOfficeCode" = 7;""";
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(1);

        command.CommandText = """SELECT "Name" FROM "ElectoralOffices" WHERE "TseOfficeCode" = 7;""";
        (await command.ExecuteScalarAsync()).Should().Be("Second");
    }

    [Fact]
    public async Task PartiesPreserveAcronymAndUpdateName()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        await repository.UpsertPartiesAsync([PoliticalParty.Create(13, "PT", "Partido dos Trabalhadores")]);
        await repository.UpsertPartiesAsync([PoliticalParty.Create(13, "PT", "Partido dos Trabalhadores Renomeado")]);

        var ids = await repository.GetPartyIdsAsync();

        ids.Should().ContainKey((13, "PT"));

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT "Name" FROM "PoliticalParties" WHERE "TsePartyNumber" = 13;""";
        (await command.ExecuteScalarAsync()).Should().Be("Partido dos Trabalhadores Renomeado");
    }

    [Fact]
    public async Task FederationsPreserveLongPartyListAcronymsAndNullComposition()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        const string partyList = "13-PT/65-PC do B/43-PV";

        await repository.UpsertFederationsAsync([
            Federation.Create(101, "FEDERAÇÃO BRASIL DA ESPERANÇA - FE BRASIL", partyList, partyList),
            Federation.Create(102, "Federação Sem Composição", "PSDB/CIDADANIA", null)
        ]);

        var ids = await repository.GetFederationIdsAsync();

        ids.Should().HaveCount(2);

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT "Acronym", "Composition" FROM "Federations" WHERE "TseFederationNumber" = 101;""";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetString(0).Should().Be(partyList);
        reader.GetString(1).Should().Be(partyList);

        await reader.CloseAsync();

        command.CommandText = """SELECT "Composition" FROM "Federations" WHERE "TseFederationNumber" = 102;""";
        (await command.ExecuteScalarAsync()).Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task OversizedValuesSurfaceTheDatabaseErrorInsteadOfTruncating()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var tooLong = new string('X', 101);

        var action = async () => await repository.UpsertOfficesAsync([
            ElectoralOffice.Create(99, tooLong)
        ]);

        await action.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == "22001");

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "ElectoralOffices" WHERE "TseOfficeCode" = 99;""";
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(0);
    }

    [Fact]
    public async Task EmptyBatchesDoNotOpenAConnection()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        (await repository.UpsertOfficesAsync([])).Should().Be(0);
        (await repository.UpsertPartiesAsync([])).Should().Be(0);
        (await repository.UpsertFederationsAsync([])).Should().Be(0);
    }

    [Fact]
    public async Task StagingTableDoesNotLeakBetweenCalls()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        await repository.UpsertOfficesAsync([ElectoralOffice.Create(1, "A")]);
        await repository.UpsertOfficesAsync([ElectoralOffice.Create(2, "B")]);

        await using var command = Db.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "ElectoralOffices";""";
        ((long)(await command.ExecuteScalarAsync())!).Should().Be(2);
    }
}
