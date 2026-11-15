using Bravo.Infrastructure.PostgreSQL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Bravo.IntegrationTests.Configurations;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly string? _externalConnectionString =
        Environment.GetEnvironmentVariable("BRAVO_TEST_POSTGRES_CONNECTION");

    private PostgreSqlContainer? _container;

    public ApiFactory Factory { get; private set; } = default!;

    public string ConnectionString => _externalConnectionString ?? _container!.GetConnectionString();

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        if (_externalConnectionString is null)
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
        }

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
