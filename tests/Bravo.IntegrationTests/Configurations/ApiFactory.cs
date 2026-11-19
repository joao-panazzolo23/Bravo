using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Geography.Entities;
using Bravo.IntegrationTests.Importing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bravo.IntegrationTests.Configurations;

public sealed class ApiFactory(PostgresContainerFixture pg) : WebApplicationFactory<Program>
{
    public TestStateSource StateSource { get; } = new();
    public TestStateValidator StateValidator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "PostgreSQL:Connection",
            pg.ConnectionString
        );
        builder.UseSetting("Importing:Worker:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(StateSource);
            services.AddSingleton(StateValidator);
            services.AddScoped<IImportPipeline>(provider => new ImportBatchPipeline<State>(
                TestStateSource.UnitKey,
                provider.GetRequiredService<TestStateSource>(),
                provider.GetRequiredService<TestStateValidator>(),
                provider.GetRequiredService<IImportBatchStore<State>>(),
                NullLogger<ImportBatchPipeline<State>>.Instance));
        });
    }
}
