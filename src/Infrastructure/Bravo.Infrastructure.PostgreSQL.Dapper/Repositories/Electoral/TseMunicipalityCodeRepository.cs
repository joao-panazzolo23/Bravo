using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class TseMunicipalityCodeRepository(NpgsqlDataSource dataSource)
    : ITseMunicipalityCodeRepository
{
    // Municipality code resolutions used to be serialized to JSON and parsed back by
    // jsonb_to_recordset. They now stream into a temporary table with binary COPY and merge with
    // one INSERT ... SELECT.
    private static readonly BulkStagingTable Staging = new(
        """
        CREATE TEMP TABLE staging_tse_municipality_codes (
            "Id" uuid,
            "RawCode" text,
            "StateId" uuid,
            "MunicipalityId" uuid,
            "ElectionYear" integer,
            "MatchMethod" text,
            "Confidence" numeric(3,2),
            "IsVerified" boolean,
            "VerifiedAt" timestamp with time zone,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_tse_municipality_codes
            ("Id", "RawCode", "StateId", "MunicipalityId", "ElectionYear", "MatchMethod",
             "Confidence", "IsVerified", "VerifiedAt", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // MunicipalityId keeps its fill-only semantics, so a later unresolved pass cannot erase an
    // earlier match. The guard compares the effective municipality plus the two mutable columns,
    // which means a re-resolved batch with identical output writes nothing.
    private const string MergeSql = """
        INSERT INTO "TseMunicipalityCodes" AS target
            ("Id", "RawCode", "StateId", "MunicipalityId", "ElectionYear",
             "MatchMethod", "Confidence", "IsVerified", "VerifiedAt", "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "RawCode", "StateId", "MunicipalityId", "ElectionYear",
            "MatchMethod", "Confidence", "IsVerified", "VerifiedAt", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("RawCode", "ElectionYear")
                   "Id", "RawCode", "StateId", "MunicipalityId", "ElectionYear",
                   "MatchMethod", "Confidence", "IsVerified", "VerifiedAt", "CreatedAt", "UpdatedAt"
            FROM staging_tse_municipality_codes
            ORDER BY "RawCode", "ElectionYear", "ctid" DESC
        ) AS staged
        ORDER BY "RawCode", "ElectionYear"
        ON CONFLICT ("RawCode", "ElectionYear") DO UPDATE SET
            "MatchMethod" = EXCLUDED."MatchMethod",
            "Confidence" = EXCLUDED."Confidence",
            "MunicipalityId" = COALESCE(target."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(
            target."MatchMethod", target."Confidence",
            COALESCE(target."MunicipalityId", EXCLUDED."MunicipalityId")
        ) IS DISTINCT FROM ROW(
            EXCLUDED."MatchMethod", EXCLUDED."Confidence", EXCLUDED."MunicipalityId"
        );
        """;

    private const string SelectSql = """
        SELECT "RawCode", "MunicipalityId"
        FROM "TseMunicipalityCodes"
        WHERE "ElectionYear" = @Year;
        """;

    public Task<int> UpsertAsync(
        IReadOnlyCollection<TseMunicipalityCode> codes,
        CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
            return Task.FromResult(0);

        return BulkCopy.UpsertAsync(
            dataSource,
            Staging,
            codes,
            WriteCodeAsync,
            MergeSql,
            cancellationToken);
    }

    private static async Task WriteCodeAsync(
        NpgsqlBinaryImporter importer,
        TseMunicipalityCode code,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(code.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(code.RawCode, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(code.StateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(code.MunicipalityId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(code.ElectionYear, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(code.MatchMethod.ToString(), NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(code.Confidence, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteAsync(code.IsVerified, NpgsqlDbType.Boolean, cancellationToken);
        await importer.WriteNullableAsync(code.VerifiedAt, NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(code.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(code.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Guid?>> GetResolvedAsync(
        int electionYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(string RawCode, Guid? MunicipalityId)>(
            new CommandDefinition(SelectSql, new { Year = electionYear }, cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.RawCode, row => row.MunicipalityId, StringComparer.Ordinal);
    }
}
