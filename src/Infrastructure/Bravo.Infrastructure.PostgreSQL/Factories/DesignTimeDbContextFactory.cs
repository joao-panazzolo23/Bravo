using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Bravo.Infrastructure.PostgreSQL.Factories;

/// <summary>
/// During design time, there is no DI Container.
/// Thats something a litte bit better to generate migrations.
/// Do not remove: No references, but is used by EF Core tools when running CLI.
/// It also needs to be public.
/// </summary>
public sealed class DesignTimeDbContextFactory(
    ) : IDesignTimeDbContextFactory<PostgreSQLContext>
{
    public PostgreSQLContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSQLContext>();
        //GetCurrentDirectory => Get startup project path, not the infrastructure project path
        //You still need to use -s parameter informing API csproj when running CLI commands
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetValue<string>("PostgreSQL:Connection");

        optionsBuilder.UseNpgsql(connectionString);

        return new PostgreSQLContext(optionsBuilder.Options);
    }
}
