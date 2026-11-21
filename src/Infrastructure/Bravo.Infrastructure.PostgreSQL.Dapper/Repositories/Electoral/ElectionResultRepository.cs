using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Entities;
using Npgsql;
using NpgsqlTypes;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Infrastructure.PostgreSQL.Dapper.Bulk;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;

internal sealed class ElectionResultRepository(NpgsqlDataSource dataSource)
    : IElectionResultRepository
{
    // Results are the highest-volume write in the import. They used to be serialized to JSON and
    // parsed back by jsonb_to_recordset; they now stream into a temporary table with binary COPY
    // and merge with one INSERT ... SELECT.
    private static readonly BulkStagingTable Staging = new(
        """
        CREATE TEMP TABLE staging_election_results (
            "Id" uuid,
            "ElectionRoundId" uuid,
            "SectionId" uuid,
            "OfficeId" uuid,
            "VoteType" smallint,
            "CandidateId" uuid,
            "PartyId" uuid,
            "TseBallotNumber" integer,
            "Votes" bigint,
            "DatasetVersionId" uuid,
            "CreatedAt" timestamp with time zone,
            "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """,
        """
        COPY staging_election_results
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
             "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """);

    // The caller already collapses repeated keys inside a batch, but DISTINCT ON makes the
    // statement safe for any input: PostgreSQL rejects an upsert that would affect one row
    // twice. Last staged row wins, matching the accumulation order of the import pipeline.
    //
    // Only Votes and UpdatedAt are mutable here, so the WHERE guard removes a write when a
    // re-imported batch reports the same totals. The conflict still takes the row lock, but no
    // new row version, WAL record or index entry is produced.
    private const string MergeSql = """
        INSERT INTO "ElectionResults" AS target
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
             "CandidateId", "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId",
             "CreatedAt", "UpdatedAt")
        SELECT
            "Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
            "CandidateId", "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId",
            "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                                "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
                   "Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                   "CandidateId", "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId",
                   "CreatedAt", "UpdatedAt"
            FROM staging_election_results
            ORDER BY "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId",
                     "ctid" DESC
        ) AS staged
        ORDER BY "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                 "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId"
        ON CONFLICT ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
        DO UPDATE SET
            "Votes" = EXCLUDED."Votes",
            "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE target."Votes" IS DISTINCT FROM EXCLUDED."Votes";
        """;

    public Task<int> UpsertResultsAsync(
        IReadOnlyCollection<ElectionResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
            return Task.FromResult(0);

        return BulkCopy.UpsertAsync(
            dataSource,
            Staging,
            results,
            WriteResultAsync,
            MergeSql,
            cancellationToken);
    }

    private static async Task WriteResultAsync(
        NpgsqlBinaryImporter importer,
        ElectionResult result,
        CancellationToken cancellationToken)
    {
        await importer.WriteAsync(result.Id, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(result.ElectionRoundId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(result.SectionId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(result.OfficeId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync((short)result.VoteType, NpgsqlDbType.Smallint, cancellationToken);
        await importer.WriteNullableAsync(result.CandidateId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteNullableAsync(result.PartyId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(result.TseBallotNumber, NpgsqlDbType.Integer, cancellationToken);
        await importer.WriteAsync(result.Votes, NpgsqlDbType.Bigint, cancellationToken);
        await importer.WriteAsync(result.DatasetVersionId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(result.CreatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(BulkCopy.AsUtc(result.UpdatedAt), NpgsqlDbType.TimestampTz, cancellationToken);
    }
}
