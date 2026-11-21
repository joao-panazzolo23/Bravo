using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

/// <summary>
/// The pair of statements that open a staging table and stream rows into it.
/// </summary>
/// <param name="CreateSql">
/// A <c>CREATE TEMP TABLE ... ON COMMIT DROP</c> statement. The staging table mirrors the
/// destination column types so the database rejects oversized or ill-typed values during COPY
/// instead of silently truncating them.
/// </param>
/// <param name="CopySql">The <c>COPY ... FROM STDIN (FORMAT BINARY)</c> statement.</param>
internal readonly record struct BulkStagingTable(string CreateSql, string CopySql);

internal delegate Task BulkRowWriter<in TRow>(
    NpgsqlBinaryImporter importer,
    TRow row,
    CancellationToken cancellationToken);

/// <summary>
/// Streams a batch into a temporary staging table with PostgreSQL binary COPY and then runs one
/// or more set-based merges in the same transaction.
/// </summary>
/// <remarks>
/// <para>
/// This helper owns transport, not business orchestration: it knows how to open one connection,
/// create one staging table, copy the rows and hand control back to the repository for the
/// merge. Deciding what to persist and in which order stays in the application layer (import
/// sources and command handlers).
/// </para>
/// <para>
/// The whole operation runs in one transaction, so a COPY or merge failure rolls the destination
/// back and drops the staging table (<c>ON COMMIT DROP</c> removes it on commit and rollback
/// alike). Staging is therefore always cleaned up on success, cancellation and failure, and a
/// temporary table can never leak rows into a later, logically independent batch.
/// </para>
/// <para>
/// A fresh staging table is created per call rather than reused across calls. Repository calls
/// are independent and connections are pooled, so reuse would need connection-scoped state that
/// silently changes isolation guarantees; the catalog cost of one small temporary table is
/// negligible next to the COPY and merge it enables.
/// </para>
/// <para>
/// Deadlocks and serialization failures are the two database errors a concurrent import can hit
/// even when the SQL is correct. The helper retries those, and only those, a bounded number of
/// times with jitter. The transaction has been rolled back by then, so a retry restarts from an
/// empty staging table and cannot duplicate data.
/// </para>
/// </remarks>
internal static class BulkCopy
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Runs a single-statement merge and returns the number of rows inserted or updated.
    /// </summary>
    public static Task<int> UpsertAsync<TRow>(
        NpgsqlDataSource dataSource,
        BulkStagingTable staging,
        IReadOnlyCollection<TRow> rows,
        BulkRowWriter<TRow> writeRow,
        string mergeSql,
        CancellationToken cancellationToken)
        => ExecuteAsync<TRow, int>(
            dataSource,
            staging,
            rows,
            writeRow,
            async (connection, transaction, token) =>
            {
                await using var merge = new NpgsqlCommand(mergeSql, connection, transaction);

                return await merge.ExecuteNonQueryAsync(token);
            },
            cancellationToken);

    /// <summary>
    /// Runs an arbitrary merge (for example two conflict targets over the same staging data) and
    /// returns whatever the merge produces.
    /// </summary>
    public static async Task<TResult> ExecuteAsync<TRow, TResult>(
        NpgsqlDataSource dataSource,
        BulkStagingTable staging,
        IReadOnlyCollection<TRow> rows,
        BulkRowWriter<TRow> writeRow,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<TResult>> merge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writeRow);
        ArgumentNullException.ThrowIfNull(merge);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteOnceAsync(
                    dataSource,
                    staging,
                    rows,
                    writeRow,
                    merge,
                    cancellationToken);
            }
            catch (PostgresException exception)
                when (attempt < MaxAttempts && IsRetryable(exception))
            {
                // Both errors mean the transaction was already rolled back, so retrying is
                // side-effect free. Jitter keeps competing retries from colliding again.
                await Task.Delay(
                    TimeSpan.FromMilliseconds(10 * attempt + Random.Shared.Next(0, 20)),
                    cancellationToken);
            }
        }
    }

    private static async Task<TResult> ExecuteOnceAsync<TRow, TResult>(
        NpgsqlDataSource dataSource,
        BulkStagingTable staging,
        IReadOnlyCollection<TRow> rows,
        BulkRowWriter<TRow> writeRow,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<TResult>> merge,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var create = new NpgsqlCommand(staging.CreateSql, connection, transaction))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var importer = await connection.BeginBinaryImportAsync(
                         staging.CopySql,
                         cancellationToken))
        {
            foreach (var row in rows)
            {
                await importer.StartRowAsync(cancellationToken);
                await writeRow(importer, row, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        var result = await merge(connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private static bool IsRetryable(PostgresException exception)
        => exception.SqlState is PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.SerializationFailure;

    /// <summary>
    /// Npgsql requires UTC for <c>timestamptz</c>. Entities are created from
    /// <see cref="DateTime.UtcNow"/>, so an unspecified kind is already UTC in practice.
    /// </summary>
    public static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
