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

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new ApiFactory(this);
        await RunMigrationsAsync();
    }

    private async Task RunMigrationsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();

        var pending = await ctx.Database.GetPendingMigrationsAsync();
        await ctx.Database.MigrateAsync();
    }
}
