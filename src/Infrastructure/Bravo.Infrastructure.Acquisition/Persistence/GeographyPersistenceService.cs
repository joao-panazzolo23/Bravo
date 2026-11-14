using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Services;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bravo.Infrastructure.Acquisition.Persistence;

public sealed class GeographyPersistenceService(
    NpgsqlDataSource dataSource,
    ILogger<GeographyPersistenceService> logger
) : IGeographyPersistenceService
{
    private const string UpsertStateSql = """
        INSERT INTO "States" ("Id", "IbgeCode", "Abbreviation", "Name", "Region", "CreatedAt", "UpdatedAt")
        VALUES (@Id, @IbgeCode, @Abbreviation, @Name, @Region, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("IbgeCode") DO UPDATE
        SET "Abbreviation" = EXCLUDED."Abbreviation",
            "Name" = EXCLUDED."Name",
            "Region" = EXCLUDED."Region",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string UpsertMunicipalitySql = """
        INSERT INTO "Municipalities" ("Id", "IbgeCode", "Name", "StateId", "CreatedAt", "UpdatedAt")
        VALUES (@Id, @IbgeCode, @Name, @StateId, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("IbgeCode") DO UPDATE
        SET "Name" = EXCLUDED."Name",
            "StateId" = EXCLUDED."StateId",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    public async Task<int> UpsertStates(IReadOnlyList<IbgeStateRecord> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var parameters = batch.Select(s => new
        {
            Id = Guid.CreateVersion7(),
            IbgeCode = s.Code,
            Abbreviation = s.Abbreviation,
            Name = s.Name,
            Region = s.Region,
            CreatedAt = now,
            UpdatedAt = now
        });

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(UpsertStateSql, parameters, cancellationToken: ct));
    }

    public async Task<int> UpsertMunicipalities(IReadOnlyList<IbgeMunicipalityRecord> batch, CancellationToken ct = default)
    {
        if (batch.Count == 0) return 0;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var statesList = await connection.QueryAsync<(Guid Id, int IbgeCode)>(
            new CommandDefinition("SELECT \"Id\", \"IbgeCode\" FROM \"States\";", cancellationToken: ct)
        );

        var stateMap = statesList.ToDictionary(x => x.IbgeCode, x => x.Id);

        var now = DateTime.UtcNow;
        var validParams = new List<object>(batch.Count);

        foreach (var m in batch)
        {
            if (!stateMap.TryGetValue(m.StateCode, out var stateId))
            {
                logger.LogWarning("State with IBGE code {StateCode} not found for municipality {MuniName} ({MuniCode}). Skipping.", m.StateCode, m.Name, m.Code);
                continue;
            }

            validParams.Add(new
            {
                Id = Guid.CreateVersion7(),
                IbgeCode = m.Code,
                Name = m.Name,
                StateId = stateId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (validParams.Count == 0) return 0;

        return await connection.ExecuteAsync(new CommandDefinition(UpsertMunicipalitySql, validParams, cancellationToken: ct));
    }
}

