using Bravo.IntegrationTests.Configurations;
using Npgsql;
using Respawn;

namespace Bravo.IntegrationTests.Abstract;

[Collection("Database")]
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly HttpClient Client;
    protected readonly NpgsqlConnection Db;
    private Respawner _respawner = default!;
    protected virtual string[] Schemas => ["public"];

    public IntegrationTestBase(PostgresContainerFixture pg)
    {
        Client = pg.Factory.CreateClient();
        Db = new NpgsqlConnection(pg.ConnectionString);
    }

    public virtual async Task InitializeAsync()
    {
        await Db.OpenAsync();

        _respawner = await Respawner.CreateAsync(Db, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = Schemas,
            TablesToIgnore =
            [
              "__EFMigrationsHistory"
            ]
        });
    }

    public async Task DisposeAsync()
    {
        await _respawner.ResetAsync(Db);
        await Db.DisposeAsync();
        Client.Dispose();
    }
}
