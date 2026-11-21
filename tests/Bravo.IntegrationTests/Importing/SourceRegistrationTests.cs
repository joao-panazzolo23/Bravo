using Bravo.Application.Importing.Sources.Ibge;
using Bravo.Domain.Importing.Repositories;
using Bravo.Domain.Importing.Services;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class SourceRegistrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public void SourcesAreRegisteredAndUniquelyKeyed()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        var keys = scope.ServiceProvider
            .GetServices<IImportSource>()
            .Select(source => source.Key)
            .ToList();

        keys.Should().Contain("TSE");
        keys.Should().Contain("IBGE");
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheWorkerRunnerAndRepositoriesResolve()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<IImportJobRepository>()
            .Should().NotBeNull();

        scope.ServiceProvider.GetRequiredService<TestImportRunner>().Should().NotBeNull();
    }

    [Fact]
    public void TheExternalHttpClientsResolveThroughTheFactory()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<TseHttpClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IbgeHttpClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IIbgeReader>().Should().NotBeNull();
    }
}
