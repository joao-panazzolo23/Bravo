using Npgsql;
using NpgsqlTypes;

namespace Bravo.Infrastructure.Acquisition.Persistence;

/// <summary>
/// Thin helper around <see cref="NpgsqlBinaryImporter"/> used to stage batches with
/// PostgreSQL binary COPY. COPY is only used to load a staging table; merging into the target
/// tables is done with set-based SQL so unique constraints and idempotency remain authoritative.
/// </summary>
internal static class BulkCopy
{
    /// <summary>
    /// Streams rows into an unlogged staging table using binary COPY.
    /// </summary>
    public static async Task WriteAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string stagingTable,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        Func<NpgsqlBinaryImporter, T, CancellationToken, Task> writeRow,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var sql = $"COPY {stagingTable} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
        await using var importer = await connection.BeginBinaryImportAsync(sql, ct);

        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            await writeRow(importer, row, ct);
        }

        await importer.CompleteAsync(ct);
    }

    public static Task Text(NpgsqlBinaryImporter importer, string value, CancellationToken ct) =>
        importer.WriteAsync(value, NpgsqlDbType.Text, ct);

    public static Task NullableText(NpgsqlBinaryImporter importer, string? value, CancellationToken ct) =>
        value is null
            ? importer.WriteNullAsync(ct)
            : importer.WriteAsync(value, NpgsqlDbType.Text, ct);

    public static Task Int(NpgsqlBinaryImporter importer, int value, CancellationToken ct) =>
        importer.WriteAsync(value, NpgsqlDbType.Integer, ct);

    public static Task NullableInt(NpgsqlBinaryImporter importer, int? value, CancellationToken ct) =>
        value is null
            ? importer.WriteNullAsync(ct)
            : importer.WriteAsync(value.Value, NpgsqlDbType.Integer, ct);

    public static Task Bool(NpgsqlBinaryImporter importer, bool value, CancellationToken ct) =>
        importer.WriteAsync(value, NpgsqlDbType.Boolean, ct);

    public static Task Guid(NpgsqlBinaryImporter importer, Guid value, CancellationToken ct) =>
        importer.WriteAsync(value, NpgsqlDbType.Uuid, ct);

    public static Task NullableGuid(NpgsqlBinaryImporter importer, Guid? value, CancellationToken ct) =>
        value is null
            ? importer.WriteNullAsync(ct)
            : importer.WriteAsync(value.Value, NpgsqlDbType.Uuid, ct);

    public static Task NullableDate(NpgsqlBinaryImporter importer, DateOnly? value, CancellationToken ct) =>
        value is null
            ? importer.WriteNullAsync(ct)
            : importer.WriteAsync(value.Value, NpgsqlDbType.Date, ct);
}
