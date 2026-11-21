using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Repositories;
using Dapper;
using Npgsql;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Binary COPY coverage for the highest-volume write in the import: section-level results.
/// </summary>
[Collection("Database")]
public sealed class ElectionResultBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    private static ElectionResult Build(
        ReferenceSeed seed,
        VoteType type = VoteType.Blank,
        Guid? candidateId = null,
        Guid? partyId = null,
        int ballot = 95,
        long votes = 10)
        => ElectionResult.Create(
            seed.RoundId, seed.SectionId, seed.OfficeId, type, candidateId, partyId,
            ballot, votes, seed.VersionId);

    [Fact]
    public async Task ResultsAreInsertedWithSmallintEnumAndBigintVotes()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        var result = Build(seed, VoteType.Null, ballot: 96, votes: 9_000_000_000);

        await repository.UpsertResultsAsync([result]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "VoteType", "Votes", "CandidateId", "PartyId" FROM "ElectionResults" WHERE "Id" = @id;
            """;
        command.Parameters.AddWithValue("id", result.Id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetInt16(0).Should().Be((short)VoteType.Null);
        reader.GetInt64(1).Should().Be(9_000_000_000);
        reader.IsDBNull(2).Should().BeTrue();
        reader.IsDBNull(3).Should().BeTrue();
    }

    [Fact]
    public async Task PartyAndBlankResultsDoNotDuplicateAcrossRuns()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        var batch = new[]
        {
            Build(seed, VoteType.Party, partyId: seed.PartyId, ballot: 13, votes: 7),
            Build(seed, VoteType.Blank, ballot: 95, votes: 3),
            Build(seed, VoteType.Null, ballot: 96, votes: 2)
        };

        for (var run = 0; run < 3; run++)
            await repository.UpsertResultsAsync(batch);

        (await CountAsync("ElectionResults", "\"DatasetVersionId\" = @version", new { version = seed.VersionId }))
            .Should().Be(3);

        (await ScalarAsync<long>(
            """SELECT COALESCE(SUM("Votes"),0)::bigint FROM "ElectionResults" WHERE "DatasetVersionId" = @version;""",
            new { version = seed.VersionId }))
            .Should().Be(12);
    }

    [Fact]
    public async Task IdenticalResultsDoNotProduceNewRowVersions()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        var result = Build(seed, VoteType.Party, partyId: seed.PartyId, ballot: 13, votes: 7);
        await repository.UpsertResultsAsync([result]);

        var before = await RowVersionAsync("ElectionResults", result.Id);

        await repository.UpsertResultsAsync([
            Build(seed, VoteType.Party, partyId: seed.PartyId, ballot: 13, votes: 7)
        ]);

        // Votes did not change, so the conditional update must skip the write. This is the
        // write-amplification reduction the migration targets for the largest table.
        (await RowVersionAsync("ElectionResults", result.Id)).Should().Be(before);
    }

    [Fact]
    public async Task ChangedVoteTotalsStillUpdateTheStoredRow()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        var result = Build(seed, VoteType.Party, partyId: seed.PartyId, ballot: 13, votes: 7);
        await repository.UpsertResultsAsync([result]);

        var before = await RowVersionAsync("ElectionResults", result.Id);

        await repository.UpsertResultsAsync([
            Build(seed, VoteType.Party, partyId: seed.PartyId, ballot: 13, votes: 42)
        ]);

        (await RowVersionAsync("ElectionResults", result.Id)).Should().NotBe(before);
        (await ScalarAsync<long>(
            """SELECT "Votes" FROM "ElectionResults" WHERE "Id" = @id;""", new { id = result.Id }))
            .Should().Be(42);
    }

    [Fact]
    public async Task DuplicateKeysInOneBatchCollapseToTheLastStagedRow()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        // PostgreSQL rejects an upsert that would affect one row twice, so the merge must
        // deduplicate before the INSERT ... ON CONFLICT.
        await repository.UpsertResultsAsync([
            Build(seed, VoteType.Blank, ballot: 95, votes: 1),
            Build(seed, VoteType.Blank, ballot: 95, votes: 99)
        ]);

        (await CountAsync("ElectionResults", "\"DatasetVersionId\" = @version", new { version = seed.VersionId }))
            .Should().Be(1);

        (await ScalarAsync<long>(
            """SELECT "Votes" FROM "ElectionResults" WHERE "DatasetVersionId" = @version;""",
            new { version = seed.VersionId }))
            .Should().Be(99);
    }

    [Fact]
    public async Task CheckConstraintsStillRejectInconsistentVoteTypes()
    {
        var seed = await SeedReferenceAsync();

        // The entity refuses an inconsistent row, so the constraint is verified at the database
        // boundary as well: a future migration that dropped it would let corrupt rows in.
        var constraint = await ScalarAsync<string?>(
            """
            SELECT conname FROM pg_constraint
            WHERE conname = 'CK_ElectionResults_NominalRequiresCandidate'
              AND conrelid = '"ElectionResults"'::regclass;
            """);

        constraint.Should().Be("CK_ElectionResults_NominalRequiresCandidate");

        var insert = async () => await Db.ExecuteAsync(
            """
            INSERT INTO "ElectionResults"
                ("Id","ElectionRoundId","SectionId","OfficeId","VoteType","CandidateId",
                 "PartyId","TseBallotNumber","Votes","DatasetVersionId","CreatedAt","UpdatedAt")
            VALUES (@Id,@RoundId,@SectionId,@OfficeId,1,NULL,NULL,1,1,@VersionId,now(),now());
            """,
            new
            {
                Id = Guid.CreateVersion7(),
                RoundId = seed.RoundId,
                SectionId = seed.SectionId,
                OfficeId = seed.OfficeId,
                VersionId = seed.VersionId
            });

        await insert.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.CheckViolation);

        // Nominal without a candidate never reaches persistence: the factory rejects it first.
        var factory = () => Build(seed, VoteType.Nominal, candidateId: null, ballot: 1, votes: 1);

        factory.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ForeignKeysStillRejectUnknownSections()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        var invalid = ElectionResult.Create(
            seed.RoundId, Guid.CreateVersion7(), seed.OfficeId, VoteType.Blank,
            null, null, 95, 1, seed.VersionId);

        var action = async () => await repository.UpsertResultsAsync([invalid]);

        await action.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task CancellationRollsBackTheWholeBatch()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await repository.UpsertResultsAsync(
            [Build(seed, VoteType.Blank, ballot: 95, votes: 1)],
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        (await CountAsync("ElectionResults", "\"DatasetVersionId\" = @version", new { version = seed.VersionId }))
            .Should().Be(0);
    }

    [Fact]
    public async Task LargeBatchesPreserveEveryRow()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        // 2,000 distinct result rows exercise the binary COPY buffer across a realistic batch.
        var batch = Enumerable.Range(1, 2_000)
            .Select(index => Build(seed, VoteType.Blank, ballot: index, votes: index))
            .ToArray();

        var affected = await repository.UpsertResultsAsync(batch);

        affected.Should().Be(2_000);
        (await CountAsync("ElectionResults", "\"DatasetVersionId\" = @version", new { version = seed.VersionId }))
            .Should().Be(2_000);
    }

    [Fact]
    public async Task ConcurrentBatchesForDisjointSectionsDoNotCorruptEachOther()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        async Task WriteAsync(int offset) =>
            await repository.UpsertResultsAsync(Enumerable.Range(offset * 100, 100)
                .Select(index => Build(seed, VoteType.Blank, ballot: index, votes: index))
                .ToArray());

        await Task.WhenAll(WriteAsync(0), WriteAsync(1), WriteAsync(2), WriteAsync(3));

        (await CountAsync("ElectionResults", "\"DatasetVersionId\" = @version", new { version = seed.VersionId }))
            .Should().Be(400);
    }

    [Fact]
    public async Task EmptyBatchIsANoOp()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<IElectionResultRepository>(scope.ServiceProvider);

        (await repository.UpsertResultsAsync([])).Should().Be(0);
    }
}
