using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Importing.Services;
using Bravo.IntegrationTests.Importing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Configurations;

public sealed class ApiFactory(PostgresContainerFixture pg) : WebApplicationFactory<Program>
{
    public FakeImportSource Source { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("PostgreSQL:Connection", pg.ConnectionString);
        builder.UseSetting("Importing:Worker:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IImportSource>(Source);
            services.AddScoped<TestImportRunner>();
        });
    }
}
