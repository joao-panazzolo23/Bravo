using System.Text.Json;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectoralReferenceRepository(NpgsqlDataSource dataSource)
    : IElectoralReferenceRepository
{
    private const string UpsertOfficesSql = """
        INSERT INTO "ElectoralOffices" ("Id", "TseOfficeCode", "Name", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."TseOfficeCode", record."Name", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "TseOfficeCode" integer, "Name" varchar(100),
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("TseOfficeCode") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertPartiesSql = """
        INSERT INTO "PoliticalParties"
            ("Id", "TsePartyNumber", "Acronym", "Name", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."TsePartyNumber", record."Acronym", record."Name",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "TsePartyNumber" integer, "Acronym" varchar(20), "Name" varchar(150),
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("TsePartyNumber", "Acronym") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertFederationsSql = """
        INSERT INTO "Federations"
            ("Id", "TseFederationNumber", "Name", "Acronym", "Composition", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."TseFederationNumber", record."Name", record."Acronym",
               record."Composition", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "TseFederationNumber" integer, "Name" varchar(150), "Acronym" varchar(20),
            "Composition" text,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("TseFederationNumber") DO UPDATE SET
            "Name" = EXCLUDED."Name",
            "Acronym" = EXCLUDED."Acronym",
            "Composition" = EXCLUDED."Composition",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectOfficesSql = """
        SELECT "TseOfficeCode", "Id" FROM "ElectoralOffices";
        """;

    private const string SelectPartiesSql = """
        SELECT "TsePartyNumber", "Acronym", "Id" FROM "PoliticalParties";
        """;

    private const string SelectFederationsSql = """
        SELECT "TseFederationNumber", "Id" FROM "Federations";
        """;

    public async Task<int> UpsertOfficesAsync(
        IReadOnlyCollection<ElectoralOffice> offices,
        CancellationToken cancellationToken = default)
    {
        if (offices.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertOfficesSql,
            new { Records = JsonSerializer.Serialize(offices.Select(office => new
            {
                office.Id,
                office.TseOfficeCode,
                office.Name,
                office.CreatedAt,
                office.UpdatedAt
            })) },
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpsertPartiesAsync(
        IReadOnlyCollection<PoliticalParty> parties,
        CancellationToken cancellationToken = default)
    {
        if (parties.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertPartiesSql,
            new { Records = JsonSerializer.Serialize(parties.Select(party => new
            {
                party.Id,
                party.TsePartyNumber,
                party.Acronym,
                party.Name,
                party.CreatedAt,
                party.UpdatedAt
            })) },
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpsertFederationsAsync(
        IReadOnlyCollection<Federation> federations,
        CancellationToken cancellationToken = default)
    {
        if (federations.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertFederationsSql,
            new { Records = JsonSerializer.Serialize(federations.Select(federation => new
            {
                federation.Id,
                federation.TseFederationNumber,
                federation.Name,
                federation.Acronym,
                federation.Composition,
                federation.CreatedAt,
                federation.UpdatedAt
            })) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetOfficeIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(int Code, Guid Id)>(new CommandDefinition(
            SelectOfficesSql,
            cancellationToken: cancellationToken));

        return rows.GroupBy(row => row.Code).ToDictionary(g => g.Key, g => g.First().Id);
    }

    public async Task<IReadOnlyDictionary<(int Number, string Acronym), Guid>> GetPartyIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<PartyKeyRow>(new CommandDefinition(
            SelectPartiesSql,
            cancellationToken: cancellationToken));

        // Acronym casing drifts between years (for example "PC do B" vs "PC DO B"), so the
        // key is case-insensitive and duplicate variants collapse to the first row.
        return rows
            .GroupBy(row => (row.TsePartyNumber, row.Acronym.ToUpperInvariant()))
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetFederationIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(int Number, Guid Id)>(new CommandDefinition(
            SelectFederationsSql,
            cancellationToken: cancellationToken));

        return rows.GroupBy(row => row.Number).ToDictionary(g => g.Key, g => g.First().Id);
    }

    private sealed record PartyKeyRow(int TsePartyNumber, string Acronym, Guid Id);
}
