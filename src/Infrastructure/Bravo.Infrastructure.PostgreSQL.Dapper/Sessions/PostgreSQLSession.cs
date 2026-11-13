using System.Data;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Sessions;

internal class PostgreSQLSession : IDisposable
{
    public IDbConnection Connection { get; }

    public PostgreSQLSession(NpgsqlDataSource dataSource)
    {
        Connection = dataSource.CreateConnection();
    }

    public void Dispose()
    {
        Connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
