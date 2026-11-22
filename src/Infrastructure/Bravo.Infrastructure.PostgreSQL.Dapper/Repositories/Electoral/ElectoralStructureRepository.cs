using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectoralStructureRepository(NpgsqlDataSource dataSource)
    : IElectoralStructureRepository
{
    // Zone, section, polling-place and assignment batches used to be serialized to JSON and
    // parsed back by jsonb_to_recordset. Each now streams into a temporary table with binary
    // COPY and merges with one INSERT ... SELECT. Staging columns repeat the destination types so
    // an oversized or ill-typed value fails during COPY with the real database error.
    private static readonly BulkStagingTable ZonesStaging = new(
        """
        CREATE TEMP TABLE staging_electoral_zones (
            "Id" uuid,
            "StateId" uuid,
            "TseZoneNumber" integer,
            "MunicipalityId" uuid,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_electoral_zones
            ("Id", "StateId", "TseZoneNumber", "MunicipalityId", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // DISTINCT ON collapses a repeated conflict key inside one batch; PostgreSQL rejects an
    // upsert that would touch one row twice. ctid ordering keeps the last staged row, which is
    // the accumulation order used by the structure pass.
    //
    // MunicipalityId is only fillable, never overwritten by a null, because that is what the
    // original COALESCE(target, EXCLUDED) meant. A no-op guard compares the effective value so a
    // re-import with nothing to fill writes nothing.
    private const string MergeZonesSql = """
        INSERT INTO "ElectoralZones" AS target
            ("Id", "StateId", "TseZoneNumber", "MunicipalityId", "CreatedAt", "UpdatedAt")
        SELECT "Id", "StateId", "TseZoneNumber", "MunicipalityId", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("StateId", "TseZoneNumber")
                   "Id", "StateId", "TseZoneNumber", "MunicipalityId", "CreatedAt", "UpdatedAt"
            FROM staging_electoral_zones
            ORDER BY "StateId", "TseZoneNumber", "ctid" DESC
        ) AS staged
        ORDER BY "StateId", "TseZoneNumber"
        ON CONFLICT ("StateId", "TseZoneNumber") DO UPDATE SET
            "MunicipalityId" = COALESCE(target."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."MunicipalityId" IS NULL
          AND EXCLUDED."MunicipalityId" IS NOT NULL;
        """;

    private static readonly BulkStagingTable SectionsStaging = new(
        """
        CREATE TEMP TABLE staging_electoral_sections (
            "Id" uuid,
            "MunicipalityId" uuid,
            "ZoneId" uuid,
            "TseSectionNumber" integer,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_electoral_sections
            ("Id", "MunicipalityId", "ZoneId", "TseSectionNumber", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // A section has no mutable business column, so the previous DO UPDATE only refreshed
    // UpdatedAt. Nothing in the application reads that timestamp as a heartbeat: sections are
    // resolved by their natural key and the caller already filters out the ones that exist. A
    // re-import therefore has no observable state change to make, and DO NOTHING avoids a row
    // version, WAL and index maintenance for every section of every repeated scope.
    private const string MergeSectionsSql = """
        INSERT INTO "ElectoralSections"
            ("Id", "MunicipalityId", "ZoneId", "TseSectionNumber", "CreatedAt", "UpdatedAt")
        SELECT "Id", "MunicipalityId", "ZoneId", "TseSectionNumber", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("MunicipalityId", "ZoneId", "TseSectionNumber")
                   "Id", "MunicipalityId", "ZoneId", "TseSectionNumber", "CreatedAt", "UpdatedAt"
            FROM staging_electoral_sections
            ORDER BY "MunicipalityId", "ZoneId", "TseSectionNumber", "ctid" DESC
        ) AS staged
        ORDER BY "MunicipalityId", "ZoneId", "TseSectionNumber"
        ON CONFLICT ("MunicipalityId", "ZoneId", "TseSectionNumber") DO NOTHING;
        """;

    private static readonly BulkStagingTable PollingPlacesStaging = new(
        """
        CREATE TEMP TABLE staging_polling_places (
            "Id" uuid,
            "StateId" uuid,
            "MunicipalityId" uuid,
            "ZoneId" uuid,
            "TsePollingPlaceNumber" integer,
            "Name" varchar(250),
            "Address" varchar(300),
            "Neighborhood" varchar(150),
            "ZipCode" text,
            "Latitude" numeric(10,7),
            "Longitude" numeric(10,7),
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_polling_places
            ("Id", "StateId", "MunicipalityId", "ZoneId", "TsePollingPlaceNumber", "Name",
             "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // Every business column is overwritten with EXCLUDED except MunicipalityId, which keeps its
    // fill-only semantics. The guard compares those same mutable columns, so an unchanged place
    // is not rewritten but a changed name, address or coordinate still is.
    private const string MergePollingPlacesSql = """
        INSERT INTO "PollingPlaces" AS target
            ("Id", "StateId", "MunicipalityId", "ZoneId", "TsePollingPlaceNumber", "Name",
             "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude", "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "StateId", "MunicipalityId", "ZoneId", "TsePollingPlaceNumber", "Name",
            "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("StateId", "ZoneId", "TsePollingPlaceNumber")
                   "Id", "StateId", "MunicipalityId", "ZoneId", "TsePollingPlaceNumber", "Name",
                   "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude",
                   "CreatedAt", "UpdatedAt"
            FROM staging_polling_places
            ORDER BY "StateId", "ZoneId", "TsePollingPlaceNumber", "ctid" DESC
        ) AS staged
        ORDER BY "StateId", "ZoneId", "TsePollingPlaceNumber"
        ON CONFLICT ("StateId", "ZoneId", "TsePollingPlaceNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Address" = EXCLUDED."Address",
            "Neighborhood" = EXCLUDED."Neighborhood",
            "ZipCode" = EXCLUDED."ZipCode",
            "Latitude" = EXCLUDED."Latitude",
            "Longitude" = EXCLUDED."Longitude",
            "MunicipalityId" = COALESCE(target."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE ROW(
            target."Name", target."Address", target."Neighborhood", target."ZipCode",
            target."Latitude", target."Longitude", target."MunicipalityId"
        ) IS DISTINCT FROM ROW(
            EXCLUDED."Name", EXCLUDED."Address", EXCLUDED."Neighborhood", EXCLUDED."ZipCode",
            EXCLUDED."Latitude", EXCLUDED."Longitude",
            COALESCE(target."MunicipalityId", EXCLUDED."MunicipalityId")
        );
        """;

    private static readonly BulkStagingTable AssignmentsStaging = new(
        """
        CREATE TEMP TABLE staging_polling_place_sections (
            "Id" uuid,
            "ElectionRoundId" uuid,
            "PollingPlaceId" uuid,
            "SectionId" uuid,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_polling_place_sections
            ("Id", "ElectionRoundId", "PollingPlaceId", "SectionId", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // The only mutable column is PollingPlaceId. Rewriting a row that already points at the same
    // place changes nothing observable, so the guard skips it.
    private const string MergeAssignmentsSql = """
        INSERT INTO "PollingPlaceSections" AS target
            ("Id", "ElectionRoundId", "PollingPlaceId", "SectionId", "CreatedAt", "UpdatedAt")
        SELECT "Id", "ElectionRoundId", "PollingPlaceId", "SectionId", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("ElectionRoundId", "SectionId")
                   "Id", "ElectionRoundId", "PollingPlaceId", "SectionId", "CreatedAt", "UpdatedAt"
            FROM staging_polling_place_sections
            ORDER BY "ElectionRoundId", "SectionId", "ctid" DESC
        ) AS staged
        ORDER BY "ElectionRoundId", "SectionId"
        ON CONFLICT ("ElectionRoundId", "SectionId") DO UPDATE SET
            "PollingPlaceId" = EXCLUDED."PollingPlaceId",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."PollingPlaceId" IS DISTINCT FROM EXCLUDED."PollingPlaceId";
        """;

    // Reads are state-scoped. A nationwide scan returns ~1M rows per (year, state) scope, which
    // is why the state filter is part of the query rather than applied in memory. Sections are
    // joined through zones because the state id lives on "ElectoralZones", not the section.
    private const string SelectZonesSql = """
        SELECT "StateId", "TseZoneNumber", "Id"
        FROM "ElectoralZones"
        WHERE "StateId" = @StateId;
        """;

    private const string SelectSectionsSql = """
        SELECT s."MunicipalityId", s."ZoneId", s."TseSectionNumber", s."Id"
        FROM "ElectoralSections" s
        INNER JOIN "ElectoralZones" z ON z."Id" = s."ZoneId"
        WHERE z."StateId" = @StateId;
        """;

    private const string SelectPollingPlacesSql = """
        SELECT "StateId", "ZoneId", "TsePollingPlaceNumber", "Id"
        FROM "PollingPlaces"
        WHERE "StateId" = @StateId;
        """;

    public Task<int> UpsertZonesAsync(
        IReadOnlyCollection<ElectoralZone> zones,
        CancellationToken cancellationToken = default)
        => UpsertAsync(ZonesStaging, zones, WriteZoneAsync, MergeZonesSql, cancellationToken);

    public Task<int> UpsertSectionsAsync(
        IReadOnlyCollection<ElectoralSection> sections,
        CancellationToken cancellationToken = default)
        => UpsertAsync(SectionsStaging, sections, WriteSectionAsync, MergeSectionsSql, cancellationToken);

    public Task<int> UpsertPollingPlacesAsync(
        IReadOnlyCollection<PollingPlace> places,
        CancellationToken cancellationToken = default)
        => UpsertAsync(PollingPlacesStaging, places, WritePollingPlaceAsync, MergePollingPlacesSql, cancellationToken);

    public Task<int> UpsertAssignmentsAsync(
        IReadOnlyCollection<PollingPlaceSection> assignments,
        CancellationToken cancellationToken = default)
        => UpsertAsync(AssignmentsStaging, assignments, WriteAssignmentAsync, MergeAssignmentsSql, cancellationToken);

    private Task<int> UpsertAsync<T>(
        BulkStagingTable staging,
        IReadOnlyCollection<T> records,
        BulkRowWriter<T> writeRow,
        string mergeSql,
        CancellationToken cancellationToken)
        => records.Count == 0
            ? Task.FromResult(0)
            : BulkCopy.UpsertAsync(dataSource, staging, records, writeRow, mergeSql, cancellationToken);

    private static async Task WriteZoneAsync(
        NpgsqlBinaryImporter importer,
        ElectoralZone zone,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(zone.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(zone.StateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(zone.TseZoneNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteNullableAsync(zone.MunicipalityId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(zone.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(zone.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private static async Task WriteSectionAsync(
        NpgsqlBinaryImporter importer,
        ElectoralSection section,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(section.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(section.MunicipalityId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(section.ZoneId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(section.TseSectionNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(section.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(section.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private static async Task WritePollingPlaceAsync(
        NpgsqlBinaryImporter importer,
        PollingPlace place,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(place.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(place.StateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(place.MunicipalityId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(place.ZoneId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(place.TsePollingPlaceNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(place.Name, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(place.Address, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(place.Neighborhood, NpgsqlDbType.Varchar, cancellationToken);
        await importer.WriteNullableAsync(place.ZipCode, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteNullableAsync(place.Latitude, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteNullableAsync(place.Longitude, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(place.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(place.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private static async Task WriteAssignmentAsync(
        NpgsqlBinaryImporter importer,
        PollingPlaceSection assignment,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(assignment.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(assignment.ElectionRoundId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(assignment.PollingPlaceId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(assignment.SectionId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(assignment.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(assignment.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid StateId, int Number), Guid>> GetZoneIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ZoneRow>(new CommandDefinition(
            SelectZonesSql, new { StateId = stateId }, cancellationToken: cancellationToken));

        return rows.ToDictionary(row => (row.StateId, row.TseZoneNumber), row => row.Id);
    }

    public async Task<IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid>> GetSectionIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SectionRow>(new CommandDefinition(
            SelectSectionsSql, new { StateId = stateId }, cancellationToken: cancellationToken));

        return rows.ToDictionary(
            row => (row.MunicipalityId, row.ZoneId, row.TseSectionNumber),
            row => row.Id);
    }

    public async Task<IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid>> GetPollingPlaceIdsAsync(
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PollingPlaceRow>(new CommandDefinition(
            SelectPollingPlacesSql, new { StateId = stateId }, cancellationToken: cancellationToken));

        return rows.ToDictionary(
            row => (row.StateId, row.ZoneId, row.TsePollingPlaceNumber),
            row => row.Id);
    }

    private sealed record ZoneRow(Guid StateId, int TseZoneNumber, Guid Id);

    private sealed record SectionRow(Guid MunicipalityId, Guid ZoneId, int TseSectionNumber, Guid Id);

    private sealed record PollingPlaceRow(Guid StateId, Guid? ZoneId, int TsePollingPlaceNumber, Guid Id);
}
