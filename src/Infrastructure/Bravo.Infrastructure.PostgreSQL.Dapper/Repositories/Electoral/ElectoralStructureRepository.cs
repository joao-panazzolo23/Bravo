using System.Text.Json;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectoralStructureRepository(NpgsqlDataSource dataSource)
    : IElectoralStructureRepository
{
    private const string UpsertZonesSql = """
        INSERT INTO "ElectoralZones"
            ("Id", "StateId", "TseZoneNumber", "MunicipalityId", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."StateId", record."TseZoneNumber", record."MunicipalityId",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "StateId" uuid, "TseZoneNumber" integer, "MunicipalityId" uuid,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("StateId", "TseZoneNumber") DO UPDATE SET
            "MunicipalityId" = COALESCE("ElectoralZones"."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertSectionsSql = """
        INSERT INTO "ElectoralSections"
            ("Id", "MunicipalityId", "ZoneId", "TseSectionNumber", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."MunicipalityId", record."ZoneId", record."TseSectionNumber",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "MunicipalityId" uuid, "ZoneId" uuid, "TseSectionNumber" integer,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("MunicipalityId", "ZoneId", "TseSectionNumber") DO UPDATE SET
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertPollingPlacesSql = """
        INSERT INTO "PollingPlaces"
            ("Id", "StateId", "MunicipalityId", "ZoneId", "TsePollingPlaceNumber", "Name",
             "Address", "Neighborhood", "ZipCode", "Latitude", "Longitude", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."StateId", record."MunicipalityId", record."ZoneId",
               record."TsePollingPlaceNumber", record."Name", record."Address",
               record."Neighborhood", record."ZipCode", record."Latitude", record."Longitude",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "StateId" uuid, "MunicipalityId" uuid, "ZoneId" uuid,
            "TsePollingPlaceNumber" integer, "Name" varchar(250), "Address" varchar(300),
            "Neighborhood" varchar(150), "ZipCode" varchar(10),
            "Latitude" numeric(10,7), "Longitude" numeric(10,7),
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("StateId", "ZoneId", "TsePollingPlaceNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Address" = EXCLUDED."Address",
            "Neighborhood" = EXCLUDED."Neighborhood",
            "ZipCode" = EXCLUDED."ZipCode",
            "Latitude" = EXCLUDED."Latitude",
            "Longitude" = EXCLUDED."Longitude",
            "MunicipalityId" = COALESCE("PollingPlaces"."MunicipalityId", EXCLUDED."MunicipalityId"),
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertAssignmentsSql = """
        INSERT INTO "PollingPlaceSections"
            ("Id", "ElectionRoundId", "PollingPlaceId", "SectionId", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."ElectionRoundId", record."PollingPlaceId", record."SectionId",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "ElectionRoundId" uuid, "PollingPlaceId" uuid, "SectionId" uuid,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionRoundId", "SectionId") DO UPDATE SET
            "PollingPlaceId" = EXCLUDED."PollingPlaceId",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectZonesSql = """
        SELECT "StateId", "TseZoneNumber", "Id" FROM "ElectoralZones";
        """;

    private const string SelectSectionsSql = """
        SELECT "MunicipalityId", "ZoneId", "TseSectionNumber", "Id" FROM "ElectoralSections";
        """;

    private const string SelectPollingPlacesSql = """
        SELECT "StateId", "ZoneId", "TsePollingPlaceNumber", "Id" FROM "PollingPlaces";
        """;

    public Task<int> UpsertZonesAsync(
        IReadOnlyCollection<ElectoralZone> zones,
        CancellationToken cancellationToken = default)
        => BulkAsync(UpsertZonesSql, zones.Select(zone => new
        {
            zone.Id,
            zone.StateId,
            zone.TseZoneNumber,
            zone.MunicipalityId,
            zone.CreatedAt,
            zone.UpdatedAt
        }), cancellationToken);

    public Task<int> UpsertSectionsAsync(
        IReadOnlyCollection<ElectoralSection> sections,
        CancellationToken cancellationToken = default)
        => BulkAsync(UpsertSectionsSql, sections.Select(section => new
        {
            section.Id,
            section.MunicipalityId,
            section.ZoneId,
            section.TseSectionNumber,
            section.CreatedAt,
            section.UpdatedAt
        }), cancellationToken);

    public Task<int> UpsertPollingPlacesAsync(
        IReadOnlyCollection<PollingPlace> places,
        CancellationToken cancellationToken = default)
        => BulkAsync(UpsertPollingPlacesSql, places.Select(place => new
        {
            place.Id,
            place.StateId,
            place.MunicipalityId,
            place.ZoneId,
            place.TsePollingPlaceNumber,
            place.Name,
            place.Address,
            place.Neighborhood,
            place.ZipCode,
            place.Latitude,
            place.Longitude,
            place.CreatedAt,
            place.UpdatedAt
        }), cancellationToken);

    public Task<int> UpsertAssignmentsAsync(
        IReadOnlyCollection<PollingPlaceSection> assignments,
        CancellationToken cancellationToken = default)
        => BulkAsync(UpsertAssignmentsSql, assignments.Select(assignment => new
        {
            assignment.Id,
            assignment.ElectionRoundId,
            assignment.PollingPlaceId,
            assignment.SectionId,
            assignment.CreatedAt,
            assignment.UpdatedAt
        }), cancellationToken);

    public async Task<IReadOnlyDictionary<(Guid StateId, int Number), Guid>> GetZoneIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ZoneRow>(new CommandDefinition(
            SelectZonesSql, cancellationToken: cancellationToken));

        return rows.ToDictionary(row => (row.StateId, row.TseZoneNumber), row => row.Id);
    }

    public async Task<IReadOnlyDictionary<(Guid MunicipalityId, Guid ZoneId, int Number), Guid>> GetSectionIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<SectionRow>(new CommandDefinition(
            SelectSectionsSql, cancellationToken: cancellationToken));

        return rows.ToDictionary(
            row => (row.MunicipalityId, row.ZoneId, row.TseSectionNumber),
            row => row.Id);
    }

    public async Task<IReadOnlyDictionary<(Guid StateId, Guid? ZoneId, int Number), Guid>> GetPollingPlaceIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PollingPlaceRow>(new CommandDefinition(
            SelectPollingPlacesSql, cancellationToken: cancellationToken));

        return rows.ToDictionary(
            row => (row.StateId, row.ZoneId, row.TsePollingPlaceNumber),
            row => row.Id);
    }

    private async Task<int> BulkAsync<T>(
        string sql,
        IEnumerable<T> records,
        CancellationToken cancellationToken)
    {
        var materialised = records.ToArray();

        if (materialised.Length == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Records = JsonSerializer.Serialize(materialised) },
            cancellationToken: cancellationToken));
    }

    private sealed record ZoneRow(Guid StateId, int TseZoneNumber, Guid Id);

    private sealed record SectionRow(Guid MunicipalityId, Guid ZoneId, int TseSectionNumber, Guid Id);

    private sealed record PollingPlaceRow(Guid StateId, Guid? ZoneId, int TsePollingPlaceNumber, Guid Id);
}
