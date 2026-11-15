using Bravo.Application.Importing;
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
    public void IbgePipelinesAreRegisteredForEachStage()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        var importTypes = scope.ServiceProvider
            .GetServices<IImportPipeline>()
            .Select(pipeline => pipeline.UnitKey)
            .ToList();

        importTypes.Should().Contain(IbgeImport.StatesUnitKey);
        importTypes.Should().Contain(IbgeImport.MunicipalitiesUnitKey);
        scope.ServiceProvider.GetRequiredService<ImportJobExecutor>().Should().NotBeNull();
    }

    [Fact]
    public void IbgeJobRunsStatesBeforeMunicipalities()
    {
        IbgeImport.UnitKeys.Should().Equal(IbgeImport.StatesUnitKey, IbgeImport.MunicipalitiesUnitKey);
    }
}
