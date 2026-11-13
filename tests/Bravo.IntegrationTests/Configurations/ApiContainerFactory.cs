using Testcontainers.PostgreSql;

namespace Bravo.IntegrationTests.Configurations;

public class ApiContainerFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

    public async ValueTask InitializeAsync() => await _db.StartAsync();
    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

}