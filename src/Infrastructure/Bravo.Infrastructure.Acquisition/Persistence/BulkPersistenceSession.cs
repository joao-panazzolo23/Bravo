using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.Acquisition.Persistence;

/// <summary>
/// Reusable PostgreSQL session for bulk batches. It owns one connection for the lifetime of
/// the scope, keeps session-local staging tables, and wraps each batch in an explicit
/// transaction so that a batch is either fully merged or fully rolled back.
///
/// No transaction is held while datasets are downloaded or parsed: a batch is only opened
/// when records are ready to persist.
/// </summary>
public abstract class BulkPersistenceSession(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    /// <summary>
    /// Command timeout for one batch. A batch is bounded by the configured batch size, so this
    /// covers one COPY and one merge statement rather than a whole dataset.
    /// </summary>
    protected virtual int CommandTimeoutSeconds => 300;

    private NpgsqlConnection? _connection;
    private bool _stagingReady;

    protected async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct)
    {
        _connection ??= await dataSource.OpenConnectionAsync(ct);

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        return _connection;
    }

    /// <summary>
    /// Creates the session staging tables once. Temporary tables are session scoped, so
    /// concurrent imports never share staging data.
    /// </summary>
    private async Task EnsureStagingAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        if (_stagingReady)
        {
            return;
        }

        await connection.ExecuteAsync(Command(StagingDdl, null, ct));
        _stagingReady = true;
    }

    protected abstract string StagingDdl { get; }

    /// <summary>
    /// Builds a command definition with the configured batch timeout.
    /// </summary>
    protected CommandDefinition Command(
        string sql,
        object? parameters,
        CancellationToken ct,
        NpgsqlTransaction? transaction = null) =>
        new(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            commandTimeout: CommandTimeoutSeconds,
            cancellationToken: ct);

    /// <summary>
    /// Runs one batch atomically: truncates the staging table, loads the rows with binary
    /// COPY and executes the set-based merge in a single transaction.
    /// </summary>
    protected async Task<TResult> ExecuteBatchAsync<TRow, TResult>(
        string stagingTable,
        IReadOnlyList<string> columns,
        IReadOnlyList<TRow> rows,
        Func<NpgsqlBinaryImporter, TRow, CancellationToken, Task> writeRow,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<TResult>> merge,
        CancellationToken ct)
    {
        var connection = await GetConnectionAsync(ct);
        await EnsureStagingAsync(connection, ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(Command($"TRUNCATE {stagingTable}", null, ct, transaction));

        await BulkCopy.WriteAsync(connection, transaction, stagingTable, columns, rows, writeRow, ct);

        var result = await merge(connection, transaction, ct);
        await transaction.CommitAsync(ct);

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
