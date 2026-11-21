using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Bravo.PersistenceBenchmarks;

/// <summary>
/// Compares the two persistence transports at the transport boundary.
/// </summary>
/// <remarks>
/// This harness measures only the transport: serialize, send, parse and merge. Both variants
/// write the same rows to the same table with the same conflict target, so the difference is
/// exactly the difference the migration introduced. Run it against a throwaway database:
///
/// <code>
/// dotnet run -c Release --project benchmarks/Bravo.PersistenceBenchmarks -- \
///     "Host=localhost;Port=5432;Database=bravo_bench;Username=postgres;Password=postgres"
/// </code>
///
/// Each measurement is repeated <see cref="Rounds"/> times after a warm-up round, and the table
/// is truncated before measuring so both variants insert rather than update.
/// </remarks>
public static class Program
{
    private const int Rounds = 5;

    private static readonly int[] BatchSizes = [100, 500, 1_000, 2_000, 5_000];

    public static async Task<int> Main(string[] args)
    {
        var connectionString = args.Length > 0
            ? args[0]
            : "Host=localhost;Port=5432;Database=bravo_bench;Username=postgres;Password=postgres";

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await PrepareSchemaAsync(dataSource);

        Console.WriteLine(
            $"{"Batch",7} {"Variant",7} {"Rows/s",11} {"Total ms",10} " +
            $"{"Serialize ms",13} {"DB ms",9} {"Payload B",12} {"Alloc B",12}");

        foreach (var batchSize in BatchSizes)
        {
            var rows = BuildRows(batchSize);

            var jsonb = await MeasureAsync(dataSource, "JSONB", rows, RunJsonbAsync);
            var copy = await MeasureAsync(dataSource, "COPY", rows, RunCopyAsync);

            Print(jsonb);
            Print(copy);
        }

        return 0;
    }

    private sealed record Row(
        Guid Id,
        Guid ElectionRoundId,
        Guid SectionId,
        Guid OfficeId,
        short VoteType,
        Guid? CandidateId,
        Guid? PartyId,
        int Ballot,
        long Votes,
        Guid VersionId,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record Measurement(
        string Variant,
        int Rows,
        double TotalMs,
        double SerializeMs,
        double DatabaseMs,
        long PayloadBytes,
        long AllocatedBytes);

    private sealed record RunResult(double SerializeMs, double DatabaseMs, long PayloadBytes);

    private static Row[] BuildRows(int count)
    {
        var roundId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var officeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var versionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = DateTime.UtcNow;

        return Enumerable.Range(1, count)
            .Select(index => new Row(
                Guid.CreateVersion7(),
                roundId,
                Guid.CreateVersion7(),
                officeId,
                VoteType: 3,
                CandidateId: null,
                PartyId: null,
                Ballot: index,
                Votes: index,
                versionId,
                now,
                now))
            .ToArray();
    }

    private static async Task<Measurement> MeasureAsync(
        NpgsqlDataSource dataSource,
        string variant,
        Row[] rows,
        Func<NpgsqlDataSource, Row[], Task<RunResult>> run)
    {
        await RunOnceAsync(dataSource, rows, run);

        var totals = new List<double>(Rounds);
        var serializes = new List<double>(Rounds);
        var databases = new List<double>(Rounds);
        long payload = 0;
        long allocated = 0;

        for (var round = 0; round < Rounds; round++)
        {
            await TruncateAsync(dataSource);

            var before = GC.GetTotalAllocatedBytes(precise: true);
            var started = Stopwatch.GetTimestamp();
            var result = await run(dataSource, rows);

            totals.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            serializes.Add(result.SerializeMs);
            databases.Add(result.DatabaseMs);
            payload = result.PayloadBytes;
            allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        }

        return new Measurement(
            variant,
            rows.Length,
            totals.Average(),
            serializes.Average(),
            databases.Average(),
            payload,
            allocated / Rounds);
    }

    private static async Task RunOnceAsync(
        NpgsqlDataSource dataSource,
        Row[] rows,
        Func<NpgsqlDataSource, Row[], Task<RunResult>> run)
    {
        await TruncateAsync(dataSource);
        await run(dataSource, rows);
    }

    private static async Task<RunResult> RunJsonbAsync(NpgsqlDataSource dataSource, Row[] rows)
    {
        var serializeStarted = Stopwatch.GetTimestamp();

        var payload = JsonSerializer.Serialize(rows.Select(row => new
        {
            row.Id,
            row.ElectionRoundId,
            row.SectionId,
            row.OfficeId,
            row.VoteType,
            row.CandidateId,
            row.PartyId,
            TseBallotNumber = row.Ballot,
            row.Votes,
            DatasetVersionId = row.VersionId,
            row.CreatedAt,
            row.UpdatedAt
        }));

        var serializeMs = Stopwatch.GetElapsedTime(serializeStarted).TotalMilliseconds;
        var databaseStarted = Stopwatch.GetTimestamp();

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            JsonbUpsertSql,
            new { Records = payload },
            transaction));

        await transaction.CommitAsync();

        return new RunResult(
            serializeMs,
            Stopwatch.GetElapsedTime(databaseStarted).TotalMilliseconds,
            System.Text.Encoding.UTF8.GetByteCount(payload));
    }

    private static async Task<RunResult> RunCopyAsync(NpgsqlDataSource dataSource, Row[] rows)
    {
        // Nothing to serialize before the connection is opened; the importer encodes directly.
        const double serializeMs = 0;
        var databaseStarted = Stopwatch.GetTimestamp();

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var create = new NpgsqlCommand(CreateStagingSql, connection, transaction))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var importer = await connection.BeginBinaryImportAsync(CopySql))
        {
            foreach (var row in rows)
            {
                await importer.StartRowAsync();
                await importer.WriteAsync(row.Id, NpgsqlDbType.Uuid);
                await importer.WriteAsync(row.ElectionRoundId, NpgsqlDbType.Uuid);
                await importer.WriteAsync(row.SectionId, NpgsqlDbType.Uuid);
                await importer.WriteAsync(row.OfficeId, NpgsqlDbType.Uuid);
                await importer.WriteAsync(row.VoteType, NpgsqlDbType.Smallint);
                await importer.WriteNullAsync();
                await importer.WriteNullAsync();
                await importer.WriteAsync(row.Ballot, NpgsqlDbType.Integer);
                await importer.WriteAsync(row.Votes, NpgsqlDbType.Bigint);
                await importer.WriteAsync(row.VersionId, NpgsqlDbType.Uuid);
                await importer.WriteAsync(row.CreatedAt, NpgsqlDbType.TimestampTz);
                await importer.WriteAsync(row.UpdatedAt, NpgsqlDbType.TimestampTz);
            }

            await importer.CompleteAsync();
        }

        await using (var merge = new NpgsqlCommand(MergeSql, connection, transaction))
        {
            await merge.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        return new RunResult(
            serializeMs,
            Stopwatch.GetElapsedTime(databaseStarted).TotalMilliseconds,
            EstimateBinaryPayload(rows.Length));
    }

    /// <summary>
    /// One row is 16+16+16+16+2+16+16+4+8+16+8+8 bytes on the wire plus the COPY header, which is
    /// the number shown for the binary variant.
    /// </summary>
    private static long EstimateBinaryPayload(int rows) => 19 + rows * 142L;

    private static void Print(Measurement measurement)
    {
        var rowsPerSecond = measurement.Rows / Math.Max(measurement.TotalMs, 0.001) * 1_000;

        Console.WriteLine(
            $"{measurement.Rows,7} {measurement.Variant,7} {rowsPerSecond,11:N0} " +
            $"{measurement.TotalMs,10:N2} {measurement.SerializeMs,13:N2} " +
            $"{measurement.DatabaseMs,9:N2} {measurement.PayloadBytes,12:N0} " +
            $"{measurement.AllocatedBytes,12:N0}");
    }

    private static async Task TruncateAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("TRUNCATE benchmark_results;");
    }

    private static async Task PrepareSchemaAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();

        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS benchmark_results (
                "Id" uuid PRIMARY KEY,
                "ElectionRoundId" uuid NOT NULL,
                "SectionId" uuid NOT NULL,
                "OfficeId" uuid NOT NULL,
                "VoteType" smallint NOT NULL,
                "CandidateId" uuid,
                "PartyId" uuid,
                "TseBallotNumber" integer NOT NULL,
                "Votes" bigint NOT NULL,
                "DatasetVersionId" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_benchmark_results_key
            ON benchmark_results
                ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                 "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
            NULLS NOT DISTINCT;

            TRUNCATE benchmark_results;
            """);
    }

    private const string JsonbUpsertSql = """
        INSERT INTO benchmark_results
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
             "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt")
        SELECT record."Id", record."ElectionRoundId", record."SectionId", record."OfficeId",
               record."VoteType", record."CandidateId", record."PartyId",
               record."TseBallotNumber", record."Votes", record."DatasetVersionId",
               record."CreatedAt", record."UpdatedAt"
        FROM jsonb_to_recordset(@Records::jsonb) AS record(
            "Id" uuid, "ElectionRoundId" uuid, "SectionId" uuid, "OfficeId" uuid,
            "VoteType" smallint, "CandidateId" uuid, "PartyId" uuid,
            "TseBallotNumber" integer, "Votes" bigint, "DatasetVersionId" uuid,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone)
        ON CONFLICT ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
        DO UPDATE SET "Votes" = EXCLUDED."Votes", "UpdatedAt" = EXCLUDED."UpdatedAt";
        """;

    private const string CreateStagingSql = """
        CREATE TEMP TABLE benchmark_staging (
            "Id" uuid, "ElectionRoundId" uuid, "SectionId" uuid, "OfficeId" uuid,
            "VoteType" smallint, "CandidateId" uuid, "PartyId" uuid,
            "TseBallotNumber" integer, "Votes" bigint, "DatasetVersionId" uuid,
            "CreatedAt" timestamp with time zone, "UpdatedAt" timestamp with time zone
        ) ON COMMIT DROP;
        """;

    private const string CopySql = """
        COPY benchmark_staging
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
             "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt")
        FROM STDIN (FORMAT BINARY);
        """;

    private const string MergeSql = """
        INSERT INTO benchmark_results
            ("Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
             "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt")
        SELECT "Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
               "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt"
        FROM (
            SELECT DISTINCT ON ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                                "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
                   "Id", "ElectionRoundId", "SectionId", "OfficeId", "VoteType", "CandidateId",
                   "PartyId", "TseBallotNumber", "Votes", "DatasetVersionId", "CreatedAt", "UpdatedAt"
            FROM benchmark_staging
            ORDER BY "ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId", "ctid" DESC
        ) AS staged
        ON CONFLICT ("ElectionRoundId", "SectionId", "OfficeId", "VoteType",
                     "CandidateId", "PartyId", "TseBallotNumber", "DatasetVersionId")
        DO UPDATE SET "Votes" = EXCLUDED."Votes", "UpdatedAt" = EXCLUDED."UpdatedAt"
        WHERE benchmark_results."Votes" IS DISTINCT FROM EXCLUDED."Votes";
        """;
}
