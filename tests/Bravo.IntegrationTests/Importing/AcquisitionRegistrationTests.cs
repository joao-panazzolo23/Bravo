using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Services;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

[Collection("Database")]
public sealed class AcquisitionRegistrationTests(PostgresContainerFixture pg) : ImportingTestBase(pg)
{
    [Fact]
    public void ProductionPipelineIsRegisteredWithTheEngine()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        var pipelines = scope.ServiceProvider.GetServices<IImportPipeline>().ToList();

        pipelines.Should().Contain(pipeline => pipeline.ImportType == "IbgeStates");
        scope.ServiceProvider.GetRequiredService<ImportJobExecutor>().Should().NotBeNull();
    }
}
