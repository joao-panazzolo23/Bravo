using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class DatasetVersioningIntegrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{

    [Fact]
    public async Task SameChecksumReusesTheDatasetRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IImportDatasetRepository>();

        var first = ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "vot-secao-2022-v1",
            "https://cdn.tse.jus.br/x.zip", "x.zip", "abc123", 100, DateTime.UtcNow);

        var second = ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "vot-secao-2022-v1",
            "https://cdn.tse.jus.br/x.zip", "x.zip", "abc123", 100, DateTime.UtcNow);

        var stored = await repository.RegisterAsync(first);
        var again = await repository.RegisterAsync(second);

        again.Id.Should().Be(stored.Id);
    }

    [Fact]
    public async Task ActivatingANewVersionSupersedesThePrevious()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var datasets = scope.ServiceProvider.GetRequiredService<IImportDatasetRepository>();
        var versions = scope.ServiceProvider.GetRequiredService<IDatasetVersionRepository>();

        const string scopeName = "TSE:votacao_secao:2022:AC:round-1";

        var datasetV1 = await datasets.RegisterAsync(ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "v1", "https://x/1.zip", "1.zip",
            "checksum-v1", 10, DateTime.UtcNow));

        var datasetV2 = await datasets.RegisterAsync(ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "v2", "https://x/2.zip", "2.zip",
            "checksum-v2", 20, DateTime.UtcNow));

        var v1 = await versions.CreateAsync(DatasetVersion.Create(datasetV1.Id, scopeName, null));
        var v2 = await versions.CreateAsync(DatasetVersion.Create(datasetV2.Id, scopeName, null));

        (await versions.ActivateAsync(v1.Id)).Should().BeTrue();
        (await versions.GetActiveAsync(scopeName))!.Id.Should().Be(v1.Id);

        (await versions.ActivateAsync(v2.Id)).Should().BeTrue();

        var active = await versions.GetActiveAsync(scopeName);
        active!.Id.Should().Be(v2.Id);
        active.IsActive.Should().BeTrue();

        (await versions.GetActiveAsync(scopeName))!.Id.Should().Be(v2.Id);
    }

    [Fact]
    public async Task OnlyOneVersionCanBeActivePerScope()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var datasets = scope.ServiceProvider.GetRequiredService<IImportDatasetRepository>();
        var versions = scope.ServiceProvider.GetRequiredService<IDatasetVersionRepository>();

        const string scopeName = "TSE:votacao_secao:2022:SP:round-1";

        var datasetA = await datasets.RegisterAsync(ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "v1", "https://x/3.zip", "3.zip",
            "checksum-sp-a", 10, DateTime.UtcNow));

        var datasetB = await datasets.RegisterAsync(ImportDataset.Create(
            "TSE", "votacao_secao", 2022, "v2", "https://x/4.zip", "4.zip",
            "checksum-sp-b", 20, DateTime.UtcNow));

        var a = await versions.CreateAsync(DatasetVersion.Create(datasetA.Id, scopeName, null));
        var b = await versions.CreateAsync(DatasetVersion.Create(datasetB.Id, scopeName, null));

        await versions.ActivateAsync(a.Id);
        await versions.ActivateAsync(b.Id);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM "DatasetVersions"
            WHERE "Scope" = @scope AND "IsActive";
            """;
        command.Parameters.AddWithValue("scope", scopeName);

        var activeCount = (long)(await command.ExecuteScalarAsync())!;

        activeCount.Should().Be(1);
    }
}
