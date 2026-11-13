using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Bravo.IntegrationTests.Configurations;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public ApiFactory Factory { get; private set; } = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new ApiFactory(this);
        await RunMigrationsAsync();
    }

    private async Task RunMigrationsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        // loga a connection string que o EF tá usando
        Console.WriteLine($"[MIGRATIONS] ConnectionString: {ctx.Database.GetConnectionString()}");

        var pending = await ctx.Database.GetPendingMigrationsAsync();
        Console.WriteLine($"[MIGRATIONS] Pending: {string.Join(", ", pending)}");

        await ctx.Database.MigrateAsync();

        var applied = await ctx.Database.GetAppliedMigrationsAsync();
        Console.WriteLine($"[MIGRATIONS] Applied: {string.Join(", ", applied)}");
    }
}

