using System.Text.Json;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class TseMunicipalityCodeRepository(NpgsqlDataSource dataSource)
    : ITseMunicipalityCodeRepository
{
    private const string UpsertSql = """
        INSERT INTO "TseMunicipalityCodes"
            ("Id", "RawCode", "StateId", "MunicipalityId", "ElectionYear",
             "MatchMethod", "Confidence", "IsVerified", "VerifiedAt", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."RawCode", record."StateId", record."MunicipalityId",
               record."ElectionYear", record."MatchMethod", record."Confidence",
               record."IsVerified", record."VerifiedAt", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "RawCode" varchar(10), "StateId" uuid, "MunicipalityId" uuid,
            "ElectionYear" integer, "MatchMethod" varchar(30), "Confidence" numeric(3,2),
            "IsVerified" boolean, "VerifiedAt" timestamp with time zone,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("RawCode", "ElectionYear") DO UPDATE SET
            "MatchMethod" = EXCLUDED."MatchMethod",
            "Confidence" = EXCLUDED."Confidence",
            "MunicipalityId" = COALESCE("TseMunicipalityCodes"."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectSql = """
        SELECT "RawCode", "MunicipalityId"
        FROM "TseMunicipalityCodes"
        WHERE "ElectionYear" = @Year;
        """;

    public async Task<int> UpsertAsync(
        IReadOnlyCollection<TseMunicipalityCode> codes,
        CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(codes.Select(code => new
        {
            code.Id,
            code.RawCode,
            code.StateId,
            code.MunicipalityId,
            code.ElectionYear,
            MatchMethod = code.MatchMethod.ToString(),
            code.Confidence,
            code.IsVerified,
            code.VerifiedAt,
            code.CreatedAt,
            code.UpdatedAt
        }));

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            cancellationToken: cancellationToken));
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
