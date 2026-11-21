using Npgsql;
using NpgsqlTypes;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

/// <summary>
/// Null-aware binary writers used by the staged COPY implementations.
/// </summary>
/// <remarks>
/// Binary COPY requires the PostgreSQL type to be supplied explicitly. Writing a nullable value
/// as <c>WriteNullAsync</c> rather than relying on a default keeps <c>NULL</c> distinct from an
/// empty string or a zero, which several of these tables depend on for their unique indexes.
/// </remarks>
internal static class NpgsqlBinaryImporterExtensions
{
    public static Task WriteNullableAsync<T>(
        this NpgsqlBinaryImporter importer,
        T? value,
        NpgsqlDbType type,
        CancellationToken cancellationToken)
        where T : struct
        => value.HasValue
            ? importer.WriteAsync(value.Value, type, cancellationToken)
            : importer.WriteNullAsync(cancellationToken);

    public static Task WriteNullableAsync(
        this NpgsqlBinaryImporter importer,
        string? value,
        NpgsqlDbType type,
        CancellationToken cancellationToken)
        => value is null
            ? importer.WriteNullAsync(cancellationToken)
            : importer.WriteAsync(value, type, cancellationToken);
}
