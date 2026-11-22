using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Ibge;
using Bravo.Application.Imports.Ibge.Abstractions;
using Bravo.Application.Imports.Ibge.Services;
using Bravo.Application.Imports.Tse;
using Bravo.Application.Imports.Tse.Models;
using Bravo.Application.Imports.Tse.Services;
using Bravo.Domain.Importing.Repositories;
using Bravo.Domain.Importing.Services;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Guards the dependency graph the refactoring established.
/// </summary>
/// <remarks>
/// These are structural assertions, not behaviour tests: they fail when a registration moves to the
/// wrong module, when a stage importer stops being resolvable, or when the infrastructure starts
/// registering application orchestrators again. That is exactly the class of regression a folder
/// reorganization tends to introduce silently.
/// </remarks>
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

    [Fact]
    public void EveryTseStageImporterResolves()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<TseReferenceImporter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TseCandidateImporter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TseResultImporter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TseStructurePreparer>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TseScopeResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TseImportSession>().Should().NotBeNull();
    }

    [Fact]
    public void TheIbgeModuleResolves()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IbgeReferenceImporter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IbgeImportSource>().Should().NotBeNull();
    }

    [Fact]
    public void TheFacadeReceivesTheThreeStages()
    {
        using var scope = Pg.Factory.Services.CreateScope();

        // The facade is a composition point, not an orchestrator with every dependency. If this
        // starts requiring repositories or readers it has regressed into the old design.
        var contract = typeof(TseImportSource).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        contract.Should().BeEquivalentTo(
            [typeof(TseReferenceImporter), typeof(TseCandidateImporter), typeof(TseResultImporter)]);
    }

    [Fact]
    public void TheIbgeFacadeReceivesOnlyTheImporter()
    {
        var contract = typeof(IbgeImportSource).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        contract.Should().BeEquivalentTo([typeof(IbgeReferenceImporter)]);
    }

    [Fact]
    public void TheResourceGovernorIsSharedAcrossScopes()
    {
        // The limits only mean something if every job execution in the process shares one
        // governor, so it must be a singleton regardless of the scope it is resolved from.
        using var first = Pg.Factory.Services.CreateScope();
        using var second = Pg.Factory.Services.CreateScope();

        var a = first.ServiceProvider.GetRequiredService<ImportResourceGovernor>();
        var b = second.ServiceProvider.GetRequiredService<ImportResourceGovernor>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void ScopedServicesAreIsolatedBetweenScopes()
    {
        // A stage importer must not be shared between two concurrent jobs, because it is resolved
        // per execution scope and would otherwise leak progress or caches across jobs.
        using var first = Pg.Factory.Services.CreateScope();
        using var second = Pg.Factory.Services.CreateScope();

        first.ServiceProvider.GetRequiredService<TseCandidateImporter>()
            .Should().NotBeSameAs(second.ServiceProvider.GetRequiredService<TseCandidateImporter>());

        first.ServiceProvider.GetRequiredService<TseImportSession>()
            .Should().NotBeSameAs(second.ServiceProvider.GetRequiredService<TseImportSession>());

        first.ServiceProvider.GetRequiredService<TseImportSource>()
            .Should().NotBeSameAs(second.ServiceProvider.GetRequiredService<TseImportSource>());
    }

    [Fact]
    public void TheImportSourceImplementationsComeFromApplication()
    {
        // Infrastructure must not register orchestrators. Both facades live in the Application
        // assembly, and this pins that the refactoring did not move the registration back. The
        // test host registers its own fake source, which is excluded by assembly.
        using var scope = Pg.Factory.Services.CreateScope();

        var applicationAssembly = typeof(TseImportSource).Assembly;

        var sources = scope.ServiceProvider
            .GetServices<IImportSource>()
            .Where(source => source.GetType().Assembly != typeof(SourceRegistrationTests).Assembly)
            .ToArray();

        sources.Should().NotBeEmpty();
        sources.Should().OnlyContain(source => source.GetType().Assembly == applicationAssembly);
    }
}
