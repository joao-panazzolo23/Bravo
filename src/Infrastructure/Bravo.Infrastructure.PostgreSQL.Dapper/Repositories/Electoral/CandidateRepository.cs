using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;
using Bravo.Domain.Electoral.Repositories;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class CandidateRepository(NpgsqlDataSource dataSource) : ICandidateRepository
{
    private const string UpsertIdentitySql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."Cpf", record."FullName", record."BirthDate",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "Cpf" varchar(11), "FullName" varchar(200), "BirthDate" date,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("Cpf") DO UPDATE SET
            "FullName" = EXCLUDED."FullName",
            "BirthDate" = EXCLUDED."BirthDate",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING "Id", "Cpf", "FullName", "BirthDate";
        """;

    private const string UpsertIdentityWithoutCpfSql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        SELECT record."Id", NULL, record."FullName", record."BirthDate",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "FullName" varchar(200), "BirthDate" date,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("FullName", "BirthDate") WHERE "Cpf" IS NULL DO UPDATE SET
            "FullName" = EXCLUDED."FullName",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING "Id", "Cpf", "FullName", "BirthDate";
        """;

    private const string UpsertCandidatesSql = """
        INSERT INTO "Candidates"
            ("Id", "ElectionId", "OfficeId", "StateId", "CandidateIdentityId",
             "TseCandidateSequential", "BallotNumber", "Name", "UrnName", "SocialName",
             "PartyId", "FederationId", "CoalitionId", "RegistrationStatus",
             "Gender", "Education", "Race", "Occupation", "BirthDate", "CreatedAt", "UpdatedAt")
        SELECT
            record."Id", record."ElectionId", record."OfficeId", record."StateId",
            record."CandidateIdentityId", record."TseCandidateSequential", record."BallotNumber",
            record."Name", record."UrnName", record."SocialName",
            record."PartyId", record."FederationId", record."CoalitionId",
            record."RegistrationStatus", record."Gender", record."Education", record."Race",
            record."Occupation", record."BirthDate", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "ElectionId" uuid, "OfficeId" uuid, "StateId" uuid,
            "CandidateIdentityId" uuid, "TseCandidateSequential" varchar(50), "BallotNumber" integer,
            "Name" varchar(200), "UrnName" varchar(200), "SocialName" varchar(200),
            "PartyId" uuid, "FederationId" uuid, "CoalitionId" uuid,
            "RegistrationStatus" varchar(100), "Gender" varchar(150), "Education" varchar(150),
            "Race" varchar(150), "Occupation" varchar(150), "BirthDate" date,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionId", "StateId", "OfficeId", "TseCandidateSequential",
                     "BallotNumber", "Name") DO UPDATE SET
            "OfficeId" = EXCLUDED."OfficeId",
            "StateId" = EXCLUDED."StateId",
            "CandidateIdentityId" = EXCLUDED."CandidateIdentityId",
            "BallotNumber" = EXCLUDED."BallotNumber",
            "Name" = EXCLUDED."Name",
            "UrnName" = EXCLUDED."UrnName",
            "SocialName" = EXCLUDED."SocialName",
            "PartyId" = EXCLUDED."PartyId",
            "FederationId" = EXCLUDED."FederationId",
            "CoalitionId" = EXCLUDED."CoalitionId",
            "RegistrationStatus" = EXCLUDED."RegistrationStatus",
            "Gender" = EXCLUDED."Gender",
            "Education" = EXCLUDED."Education",
            "Race" = EXCLUDED."Race",
            "Occupation" = EXCLUDED."Occupation",
            "BirthDate" = EXCLUDED."BirthDate",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string SelectCandidateIdsSql = """
        SELECT "TseCandidateSequential", "Id"
        FROM "Candidates"
        WHERE "ElectionId" = @ElectionId
          AND "StateId" = @StateId;
        """;

    public async Task<IReadOnlyDictionary<string, Guid>> UpsertIdentitiesAsync(
        IReadOnlyCollection<CandidateIdentity> identities,
        CancellationToken cancellationToken = default)
    {
        if (identities.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.Ordinal);

        var withCpf = identities.Where(identity => identity.Cpf is not null).ToArray();
        var withoutCpf = identities.Where(identity => identity.Cpf is null).ToArray();

        var result = new Dictionary<string, Guid>(identities.Count, StringComparer.Ordinal);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (withCpf.Length > 0)
        {
            var rows = await InsertIdentitiesAsync(connection, UpsertIdentitySql, withCpf, cancellationToken);

            foreach (var row in rows)
                result[CandidateIdentityKey.Of(row.Cpf, row.FullName, row.BirthDate)] = row.Id;
        }

        if (withoutCpf.Length > 0)
        {
            var rows = await InsertIdentitiesAsync(
                connection, UpsertIdentityWithoutCpfSql, withoutCpf, cancellationToken);

            foreach (var row in rows)
                result[CandidateIdentityKey.Of(null, row.FullName, row.BirthDate)] = row.Id;
        }

        return result;
    }

    private static async Task<IReadOnlyList<IdentityRow>> InsertIdentitiesAsync(
        NpgsqlConnection connection,
        string sql,
        IReadOnlyCollection<CandidateIdentity> identities,
        CancellationToken cancellationToken)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(identities.Select(identity => new
        {
            identity.Id,
            identity.Cpf,
            identity.FullName,
            identity.BirthDate,
            identity.CreatedAt,
            identity.UpdatedAt
        }));

        var rows = await connection.QueryAsync<IdentityRow>(new CommandDefinition(
            sql,
            new { Records = payload },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    private sealed record IdentityRow(Guid Id, string? Cpf, string FullName, DateOnly? BirthDate);

    public async Task<int> UpsertCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = System.Text.Json.JsonSerializer.Serialize(candidates.Select(candidate => new
        {
            candidate.Id,
            candidate.ElectionId,
            candidate.OfficeId,
            candidate.StateId,
            candidate.CandidateIdentityId,
            candidate.TseCandidateSequential,
            candidate.BallotNumber,
            candidate.Name,
            candidate.UrnName,
            candidate.SocialName,
            candidate.PartyId,
            candidate.FederationId,
            candidate.CoalitionId,
            candidate.RegistrationStatus,
            candidate.Gender,
            candidate.Education,
            candidate.Race,
            candidate.Occupation,
            candidate.BirthDate,
            candidate.CreatedAt,
            candidate.UpdatedAt
        }));

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertCandidatesSql,
            new { Records = payload },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        Guid stateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(string Sequential, Guid Id)>(new CommandDefinition(
            SelectCandidateIdsSql,
            new { ElectionId = electionId, StateId = stateId },
            cancellationToken: cancellationToken));

        // A candidate sequential is only unique within an election and state, and historical
        // files can still repeat it; first occurrence wins rather than throwing.
        return rows
            .GroupBy(row => row.Sequential, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
    }
}
