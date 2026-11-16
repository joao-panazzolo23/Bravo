using System.Text.Json;
using Bravo.Application.Electoral.Contracts;
using Bravo.Domain.Electoral.Entities;
using Dapper;
using Npgsql;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectionResultRepository(NpgsqlDataSource dataSource)
    : IElectionResultRepository
{
    private const string UpsertSql = """
        INSERT INTO "ElectionResults"
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
             "CandidateId", "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId",
             "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."ElectionRoundId", record."SectionId", record."OfficeId",
               record."VoteType", record."CandidateId", record."PartyId", record."TseBallotNumber",
               record."Votes", record."DatasetVersionId", record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "ElectionRoundId" uuid, "SectionId" uuid, "OfficeId" uuid,
            "VoteType" smallint, "CandidateId" uuid, "PartyId" uuid,
            "TseBallotNumber" integer, "Votes" bigint, "DatasetVersionId" uuid,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
        DO UPDATE SET
            "Votes" = EXCLUDED."Votes",
            "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    public async Task<int> UpsertResultsAsync(
        IReadOnlyCollection<ElectionResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
            return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(results.Select(result => new
        {
            result.Id,
            result.ElectionRoundId,
            result.SectionId,
            result.OfficeId,
            VoteType = (short)result.VoteType,
            result.CandidateId,
            result.PartyId,
            result.TseBallotNumber,
            result.Votes,
            result.DatasetVersionId,
            result.CreatedAt,
            result.UpdatedAt
        }));

        return await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { Records = payload },
            cancellationToken: cancellationToken));    }
}
