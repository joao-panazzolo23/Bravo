using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Services;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.Acquisition.Persistence;


///damn that AI with god object & not a single separation of concerns between layers

/// <summary>
/// Bulk persistence of IBGE geography.
///
/// Each batch is loaded into a session-local staging table with binary COPY and then merged
/// into "States"/"Municipalities" with a single set-based statement. Running the same import
/// again updates the existing rows through the natural keys ("IbgeCode"), so the operation is
/// idempotent and never creates duplicates.
/// </summary>
public sealed class GeographyPersistenceService(NpgsqlDataSource dataSource)
    : BulkPersistenceSession(dataSource), IGeographyPersistenceService
{
    private const string StatesStaging = "staging_states";
    private const string MunicipalitiesStaging = "staging_municipalities";
    private const string CrosswalkStaging = "staging_municipality_tse_codes";

    private static readonly string[] StateColumns = ["id", "ibge_code", "abbreviation", "name", "region"];
    private static readonly string[] MunicipalityColumns = ["id", "ibge_code", "name", "state_ibge_code"];
    private static readonly string[] CrosswalkColumns = ["state_abbreviation", "tse_code", "ibge_code", "tse_name", "meso_region", "micro_region"];

    protected override string StagingDdl => """
        CREATE TEMP TABLE IF NOT EXISTS staging_states (
            id uuid NOT NULL,
            ibge_code integer NOT NULL,
            abbreviation text NOT NULL,
            name text NOT NULL,
            region text NOT NULL
        ) ON COMMIT DELETE ROWS;

        CREATE TEMP TABLE IF NOT EXISTS staging_municipalities (
            id uuid NOT NULL,
            ibge_code integer NOT NULL,
            name text NOT NULL,
            state_ibge_code integer NOT NULL
        ) ON COMMIT DELETE ROWS;

        CREATE TEMP TABLE IF NOT EXISTS staging_municipality_tse_codes (
            state_abbreviation text NOT NULL,
            tse_code text NOT NULL,
            ibge_code integer NOT NULL,
            tse_name text NULL,
            meso_region text NULL,
            micro_region text NULL
        ) ON COMMIT DELETE ROWS;
        """;

    private const string MergeStatesSql = """
        WITH merged AS (
            INSERT INTO "States" ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), s.ibge_code, s.abbreviation, s.name, s.region, now(), now()
            FROM staging_states s
            ON CONFLICT ("IbgeCode") DO UPDATE
            SET "Abbreviation" = EXCLUDED."Abbreviation",
                "Name" = EXCLUDED."Name",
                "Region" = EXCLUDED."Region",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT count(*)::int AS processed, (count(*) FILTER (WHERE inserted))::int AS inserted
        FROM merged;
        """;

    private const string MergeMunicipalitiesSql = """
        WITH source AS (
            SELECT DISTINCT ON (m.ibge_code)
                   m.ibge_code,
                   m.name,
                   st."Id" AS state_id
            FROM staging_municipalities m
            JOIN "States" st ON st."IbgeCode" = m.state_ibge_code
            ORDER BY m.ibge_code, m.name
        ),
        merged AS (
            INSERT INTO "Municipalities" ("Id", "IbgeCode", "Name", "StateId", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), source.ibge_code, source.name, source.state_id, now(), now()
            FROM source
            ON CONFLICT ("IbgeCode") DO UPDATE
            SET "Name" = EXCLUDED."Name",
                "StateId" = EXCLUDED."StateId",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM source)::int AS processed,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    private const string MergeCrosswalkSql = """
        WITH source AS (
            SELECT DISTINCT ON (c.tse_code)
                   c.tse_code,
                   c.ibge_code,
                   c.tse_name,
                   c.meso_region,
                   c.micro_region,
                   st."Id" AS state_id,
                   m."Id" AS municipality_id
            FROM staging_municipality_tse_codes c
            JOIN "States" st ON st."Abbreviation" = c.state_abbreviation
            JOIN "Municipalities" m ON m."IbgeCode" = c.ibge_code
            ORDER BY c.tse_code
        ),
        merged AS (
            INSERT INTO "MunicipalityTseCodes"
                ("Id", "StateId", "TseCode", "IbgeCode", "MunicipalityId", "TseName", "MesoRegionName", "MicroRegionName", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), source.state_id, source.tse_code, source.ibge_code, source.municipality_id,
                   source.tse_name, source.meso_region, source.micro_region, now(), now()
            FROM source
            ON CONFLICT ("TseCode") DO UPDATE
            SET "StateId" = EXCLUDED."StateId",
                "IbgeCode" = EXCLUDED."IbgeCode",
                "MunicipalityId" = EXCLUDED."MunicipalityId",
                "TseName" = EXCLUDED."TseName",
                "MesoRegionName" = EXCLUDED."MesoRegionName",
                "MicroRegionName" = EXCLUDED."MicroRegionName",
                "UpdatedAt" = now()
            RETURNING (xmax = 0) AS inserted
        )
        SELECT (SELECT count(*) FROM source)::int AS processed,
               (SELECT count(*) FROM merged WHERE inserted)::int AS inserted;
        """;

    public Task<PersistenceResult> UpsertStates(IReadOnlyList<IbgeStateRecord> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(PersistenceResult.Empty);
        }

        var rows = batch
            .Where(s => s.Code > 0 && !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new StateRow(Guid.CreateVersion7(), s.Code, s.Abbreviation ?? string.Empty, s.Name, s.Region ?? string.Empty))
            .ToList();

        if (rows.Count == 0)
        {
            return Task.FromResult(new PersistenceResult(0, 0, batch.Count));
        }

        return ExecuteBatchAsync(
            StatesStaging,
            StateColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Guid(importer, row.Id, token);
                await BulkCopy.Int(importer, row.IbgeCode, token);
                await BulkCopy.Text(importer, row.Abbreviation, token);
                await BulkCopy.Text(importer, row.Name, token);
                await BulkCopy.Text(importer, row.Region, token);
            },
            async (connection, transaction, token) =>
            {
                var result = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeStatesSql,
                    null,
                    token,
                    transaction));

                var skipped = batch.Count - result.Processed;
                return new PersistenceResult(result.Inserted, result.Processed - result.Inserted, skipped);
            },
            ct);
    }

    public Task<PersistenceResult> UpsertMunicipalities(IReadOnlyList<IbgeMunicipalityRecord> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(PersistenceResult.Empty);
        }

        var rows = batch
            .Where(m => m.Code > 0 && !string.IsNullOrWhiteSpace(m.Name) && m.StateCode > 0)
            .Select(m => new MunicipalityRow(Guid.CreateVersion7(), m.Code, m.Name, m.StateCode))
            .ToList();

        if (rows.Count == 0)
        {
            return Task.FromResult(new PersistenceResult(0, 0, batch.Count));
        }

        return ExecuteBatchAsync(
            MunicipalitiesStaging,
            MunicipalityColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Guid(importer, row.Id, token);
                await BulkCopy.Int(importer, row.IbgeCode, token);
                await BulkCopy.Text(importer, row.Name, token);
                await BulkCopy.Int(importer, row.StateIbgeCode, token);
            },
            async (connection, transaction, token) =>
            {
                var result = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeMunicipalitiesSql,
                    null,
                    token,
                    transaction));

                var skipped = batch.Count - result.Processed;
                return new PersistenceResult(result.Inserted, result.Processed - result.Inserted, skipped);
            },
            ct);
    }

    public Task<PersistenceResult> UpsertMunicipalityTseCodes(
        IReadOnlyList<MunicipalityCrosswalkRecord> batch,
        CancellationToken ct = default)
    {
        if (batch.Count == 0)
        {
            return Task.FromResult(PersistenceResult.Empty);
        }

        var rows = batch
            .Where(c => !string.IsNullOrWhiteSpace(c.TseCode) && c.IbgeCode > 0)
            .Select(c => new CrosswalkRow(c.StateAbbreviation, c.TseCode, c.IbgeCode, c.TseName, c.MesoRegionName, c.MicroRegionName))
            .ToList();

        if (rows.Count == 0)
        {
            return Task.FromResult(new PersistenceResult(0, 0, batch.Count));
        }

        return ExecuteBatchAsync(
            CrosswalkStaging,
            CrosswalkColumns,
            rows,
            async (importer, row, token) =>
            {
                await BulkCopy.Text(importer, row.StateAbbreviation, token);
                await BulkCopy.Text(importer, row.TseCode, token);
                await BulkCopy.Int(importer, row.IbgeCode, token);
                await BulkCopy.NullableText(importer, row.TseName, token);
                await BulkCopy.NullableText(importer, row.MesoRegion, token);
                await BulkCopy.NullableText(importer, row.MicroRegion, token);
            },
            async (connection, transaction, token) =>
            {
                var result = await connection.QuerySingleAsync<MergeCounters>(Command(
                    MergeCrosswalkSql,
                    null,
                    token,
                    transaction));

                var skipped = batch.Count - result.Processed;
                return new PersistenceResult(result.Inserted, result.Processed - result.Inserted, skipped);
            },
            ct);
    }

    private sealed record StateRow(Guid Id, int IbgeCode, string Abbreviation, string Name, string Region);

    public async Task<GeographyReadiness> GetReadiness(CancellationToken ct = default)
    {
        const string sql = """
            SELECT (SELECT count(*) FROM "States")::int AS states,
                   (SELECT count(*) FROM "Municipalities")::int AS municipalities;
            """;

        var connection = await GetConnectionAsync(ct);
        return await connection.QuerySingleAsync<GeographyReadiness>(Command(sql, null, ct));
    }
    private sealed record MunicipalityRow(Guid Id, int IbgeCode, string Name, int StateIbgeCode);
    private sealed record CrosswalkRow(string StateAbbreviation, string TseCode, int IbgeCode, string? TseName, string? MesoRegion, string? MicroRegion);

    private sealed record MergeCounters(int Processed, int Inserted);
}
