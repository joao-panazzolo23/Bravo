using Bravo.Application.Electoral.Contracts;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class CandidateRepository(NpgsqlDataSource dataSource) : ICandidateRepository
{
    private const string UpsertIdentitySql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        VALUES (@Id, @Cpf, @FullName, @BirthDate, @CreatedAt, @UpdatedAt)
        ON CONFLICT ("Cpf") DO UPDATE SET
            "FullName" = EXCLUDED."FullName",
            "BirthDate" = EXCLUDED."BirthDate",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        RETURNING "Id";
        """;

    private const string InsertIdentityWithoutCpfSql = """
        INSERT INTO "CandidateIdentities" ("Id", "Cpf", "FullName", "BirthDate", "CreatedAt", "UpdatedAt")
        VALUES (@Id, NULL, @FullName, @BirthDate, @CreatedAt, @UpdatedAt)
        RETURNING "Id";
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
            "CandidateIdentityId" uuid, "TseCandidateSequential" bigint, "BallotNumber" integer,
            "Name" varchar(200), "UrnName" varchar(200), "SocialName" varchar(200),
            "PartyId" uuid, "FederationId" uuid, "CoalitionId" uuid,
            "RegistrationStatus" varchar(100), "Gender" varchar(150), "Education" varchar(150),
            "Race" varchar(150), "Occupation" varchar(150), "BirthDate" date,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionId", "TseCandidateSequential") DO UPDATE SET
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
        WHERE "ElectionId" = @ElectionId;
        """;

    public async Task<Guid> UpsertIdentityAsync(
        CandidateIdentity identity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var sql = identity.Cpf is null ? InsertIdentityWithoutCpfSql : UpsertIdentitySql;

        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(
            sql,
            new
            {
                identity.Id,
                identity.Cpf,
                identity.FullName,
                identity.BirthDate,
                identity.CreatedAt,
                identity.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

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

    public async Task<IReadOnlyDictionary<long, Guid>> GetCandidateIdsAsync(
        Guid electionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<(long Sequential, Guid Id)>(new CommandDefinition(
            SelectCandidateIdsSql,
            new { ElectionId = electionId },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.Sequential, row => row.Id);
    }
}
