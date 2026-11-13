using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bravo.IntegrationTests.Configurations;

public sealed class ApiFactory(PostgresContainerFixture pg) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "PostgreSQL:Connection",
            pg.ConnectionString
        );

    }
}