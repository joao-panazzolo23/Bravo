using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Bravo.IntegrationTests.Configurations;
using Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Pins the insert-only persistence contract of <c>CandidateIdentities</c>.
/// </summary>
/// <remarks>
/// The entity is immutable, so the repository must insert what is missing and resolve everything
/// else to the canonical row. These tests run against a real PostgreSQL instance because the
/// behaviour under test is entirely database behaviour: unique-index arbitration, the partial
/// index with <c>NULLS NOT DISTINCT</c>, visibility of rows committed by a competing transaction,
/// and whether a statement actually produced a new row version.
/// </remarks>
[Collection("Database")]
public sealed class CandidateIdentityImmutabilityTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    private ICandidateRepository CreateRepository(SqlStatementCounter counter)
    {
        var dataSource = counter.BuildDataSource(Pg.ConnectionString);

        return new CandidateRepository(dataSource);
    }

    [Fact]
    public async Task NewIdentitiesAreInsertedAndMapped()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var withCpf = CandidateIdentity.Create("123.456.789-01", "MARIA DA SILVA", new DateOnly(1980, 5, 4));
        var withoutCpf = CandidateIdentity.Create(null, "JOSE SEM CPF", null);

        var resolution = await repository.EnsureIdentitiesAsync([withCpf, withoutCpf]);

        resolution.IdsByKey.Should().HaveCount(2);
        resolution.Conflicts.Should().BeEmpty();
        resolution.IdsByKey[CandidateIdentityKey.Of("12345678901", "MARIA DA SILVA", null)]
            .Should().Be(withCpf.Id);
        resolution.IdsByKey[CandidateIdentityKey.Of(null, "JOSE SEM CPF", null)]
            .Should().Be(withoutCpf.Id);

        (await CountAsync("CandidateIdentities", "TRUE", new { })).Should().Be(2);
    }

    [Fact]
    public async Task ExistingIdentityIsResolvedToTheStoredRowAndNotRewritten()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var original = CandidateIdentity.Create("11122233344", "CANONICAL NAME", new DateOnly(1970, 1, 1));

        await repository.EnsureIdentitiesAsync([original]);

        var before = await RowVersionAsync("CandidateIdentities", original.Id);

        // A different entity instance for the same person, as a later year would produce.
        var repeated = CandidateIdentity.Create("111.222.333-44", "CANONICAL NAME", new DateOnly(1970, 1, 1));

        var resolution = await repository.EnsureIdentitiesAsync([repeated]);

        resolution.IdsByKey.Values.Should().ContainSingle().Which.Should().Be(original.Id);
        resolution.Conflicts.Should().BeEmpty();

        // The physical proof: no new row version means no UPDATE was executed.
        (await RowVersionAsync("CandidateIdentities", original.Id)).Should().Be(before);
        (await CountAsync("CandidateIdentities", "TRUE", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ExistingIdentityWithDifferentAttributesKeepsTheCanonicalValues()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var canonical = CandidateIdentity.Create("55566677788", "CANONICAL NAME", new DateOnly(1970, 1, 1));

        await repository.EnsureIdentitiesAsync([canonical]);

        var before = await RowVersionAsync("CandidateIdentities", canonical.Id);

        var conflicting = CandidateIdentity.Create("55566677788", "DIFFERENT NAME", new DateOnly(1990, 2, 2));

        var resolution = await repository.EnsureIdentitiesAsync([conflicting]);

        resolution.IdsByKey.Values.Should().ContainSingle().Which.Should().Be(canonical.Id);

        // The divergence is reported, never applied. No CPF is exposed by the diagnostic.
        var conflict = resolution.Conflicts.Should().ContainSingle().Subject;
        conflict.CandidateIdentityId.Should().Be(canonical.Id);
        conflict.FullNameDiffers.Should().BeTrue();
        conflict.BirthDateDiffers.Should().BeTrue();
        conflict.ToString().Should().NotContain("55566677788");

        (await RowVersionAsync("CandidateIdentities", canonical.Id)).Should().Be(before);
        (await ScalarAsync<string>(
            """SELECT "FullName" FROM "CandidateIdentities" WHERE "Id" = @id;""", new { id = canonical.Id }))
            .Should().Be("CANONICAL NAME");
        (await ScalarAsync<DateOnly>(
            """SELECT "BirthDate" FROM "CandidateIdentities" WHERE "Id" = @id;""", new { id = canonical.Id }))
            .Should().Be(new DateOnly(1970, 1, 1));
    }

    [Fact]
    public async Task ExistingIdentityWithIdenticalAttributesReportsNoConflict()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var canonical = CandidateIdentity.Create("77788899900", "SAME NAME", new DateOnly(1965, 6, 7));

        await repository.EnsureIdentitiesAsync([canonical]);

        var resolution = await repository.EnsureIdentitiesAsync(
            [CandidateIdentity.Create("77788899900", "SAME NAME", new DateOnly(1965, 6, 7))]);

        resolution.Conflicts.Should().BeEmpty();
        resolution.IdsByKey.Values.Should().ContainSingle().Which.Should().Be(canonical.Id);
    }

    [Fact]
    public async Task DuplicateCpfWithinOneBatchCollapsesIntoOneRow()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        // The same person registered twice in one file. PostgreSQL rejects a statement that would
        // affect one row twice, so the batch must collapse before it reaches the conflict target.
        var first = CandidateIdentity.Create("33344455566", "FIRST VARIANT", null);
        var second = CandidateIdentity.Create("33344455566", "SECOND VARIANT", null);

        var resolution = await repository.EnsureIdentitiesAsync([first, second]);

        resolution.IdsByKey.Should().HaveCount(1);
        resolution.Conflicts.Should().BeEmpty();

        (await CountAsync("CandidateIdentities", "\"Cpf\" = '33344455566'", new { })).Should().Be(1);

        // The last staged row wins, matching the accumulation order of the import source.
        (await ScalarAsync<string>(
            """SELECT "FullName" FROM "CandidateIdentities" WHERE "Cpf" = '33344455566';"""))
            .Should().Be("SECOND VARIANT");
    }

    [Fact]
    public async Task MultipleCandidatesSharingOneCpfResolveToTheSameIdentity()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        // One person registered for several offices: the identity is shared, the candidacies are
        // not. The repository must return one id no matter how many rows map to the same CPF.
        var identities = Enumerable.Range(0, 5)
            .Select(_ => CandidateIdentity.Create("99911122233", "SHARED PERSON", new DateOnly(1975, 3, 3)))
            .ToArray();

        var resolution = await repository.EnsureIdentitiesAsync(identities);

        resolution.IdsByKey.Should().ContainSingle();
        (await CountAsync("CandidateIdentities", "\"Cpf\" = '99911122233'", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentInsertionOfTheSameCpfProducesOneRowAndOneId()
    {
        var connectionString = Pg.ConnectionString;

        // Separate callers, separate data sources and separate connections: this is the shape of
        // two state imports racing for the same person in the same year.
        var tasks = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            var counter = new SqlStatementCounter();
            await using var dataSource = counter.BuildDataSource(connectionString);
            var repository = new CandidateRepository(dataSource);

            return await repository.EnsureIdentitiesAsync(
                [CandidateIdentity.Create("44455566677", "RACING PERSON", new DateOnly(1985, 8, 8))]);
        }));

        var resolutions = await Task.WhenAll(tasks);

        resolutions.SelectMany(resolution => resolution.IdsByKey.Values)
            .Distinct()
            .Should().ContainSingle("every caller must observe the winning row");

        (await CountAsync("CandidateIdentities", "\"Cpf\" = '44455566677'", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentOverlappingBatchesConvergeOnTheSameIdentitySet()
    {
        const int people = 25;

        var connectionString = Pg.ConnectionString;

        // Each caller submits the same people plus one unique person, so the batches overlap
        // without being identical: the lookup must observe both its own inserts and the rows a
        // competitor committed first.
        var tasks = Enumerable.Range(0, 8).Select(caller => Task.Run(async () =>
        {
            var counter = new SqlStatementCounter();
            await using var dataSource = counter.BuildDataSource(connectionString);
            var repository = new CandidateRepository(dataSource);

            var identities = Enumerable.Range(0, people)
                .Select(index => CandidateIdentity.Create(
                    $"1234567{index:D4}", $"OVERLAP PERSON {index}", new DateOnly(1980, 1, 1)))
                .Append(CandidateIdentity.Create(
                    $"9876543{caller:D4}", $"UNIQUE PERSON {caller}", new DateOnly(1990, 1, 1)))
                .ToArray();

            return await repository.EnsureIdentitiesAsync(identities);
        }));

        var resolutions = await Task.WhenAll(tasks);

        // Every caller must resolve every shared person to the same id.
        foreach (var index in Enumerable.Range(0, people))
        {
            var key = CandidateIdentityKey.Of(
                $"1234567{index:D4}", $"OVERLAP PERSON {index}", new DateOnly(1980, 1, 1));

            resolutions
                .Select(resolution => resolution.IdsByKey[key])
                .Distinct()
                .Should().ContainSingle($"person {index} must have exactly one id");
        }

        (await CountAsync("CandidateIdentities", "\"FullName\" LIKE 'OVERLAP PERSON %'", new { }))
            .Should().Be(people);
        (await CountAsync("CandidateIdentities", "\"FullName\" LIKE 'UNIQUE PERSON %'", new { }))
            .Should().Be(8);
    }

    [Fact]
    public async Task IdMappingCoversBothNewAndExistingIdentitiesInOneBatch()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var existing = CandidateIdentity.Create("10020030040", "ALREADY THERE", new DateOnly(1955, 5, 5));

        await repository.EnsureIdentitiesAsync([existing]);

        var fresh = CandidateIdentity.Create("10020030050", "BRAND NEW", new DateOnly(1999, 9, 9));

        var resolution = await repository.EnsureIdentitiesAsync([
            CandidateIdentity.Create("10020030040", "ALREADY THERE", new DateOnly(1955, 5, 5)),
            fresh
        ]);

        resolution.IdsByKey.Should().HaveCount(2);
        resolution.IdsByKey[CandidateIdentityKey.Of("10020030040", "ALREADY THERE", null)]
            .Should().Be(existing.Id);
        resolution.IdsByKey[CandidateIdentityKey.Of("10020030050", "BRAND NEW", null)]
            .Should().Be(fresh.Id);
    }

    [Fact]
    public async Task CpfLessIdentitiesUseThePartialIndexAndPreserveCanonicalData()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        // TSE emits "-4" for withheld CPFs. It is not eleven digits, so the entity discards it and
        // the row is unique by name and birth date. The partial index treats NULL dates as equal.
        var canonical = CandidateIdentity.Create("-4", "WITHHELD CPF", null);

        await repository.EnsureIdentitiesAsync([canonical]);

        var before = await RowVersionAsync("CandidateIdentities", canonical.Id);

        var resolution = await repository.EnsureIdentitiesAsync(
            [CandidateIdentity.Create("-4", "WITHHELD CPF", null)]);

        resolution.IdsByKey[CandidateIdentityKey.Of(null, "WITHHELD CPF", null)]
            .Should().Be(canonical.Id);
        (await RowVersionAsync("CandidateIdentities", canonical.Id)).Should().Be(before);
        (await CountAsync("CandidateIdentities", "TRUE", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ReimportIsIdempotentAndWritesNothing()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var identities = Enumerable.Range(0, 20)
            .Select(index => CandidateIdentity.Create(
                $"777000{index:D5}", $"REIMPORT PERSON {index}", new DateOnly(1970, 1, 1)))
            .ToArray();

        await repository.EnsureIdentitiesAsync(identities);

        var before = await RowVersionsAsync("CandidateIdentities", "TRUE", new { });

        var resolution = await repository.EnsureIdentitiesAsync(identities);

        resolution.IdsByKey.Should().HaveCount(identities.Length);
        resolution.Conflicts.Should().BeEmpty();

        var after = await RowVersionsAsync("CandidateIdentities", "TRUE", new { });

        // Every row version must be unchanged: a re-import is insert-only.
        after.Should().Equal(before);
        (await CountAsync("CandidateIdentities", "TRUE", new { })).Should().Be(identities.Length);
    }

    [Fact]
    public async Task IdentityBatchCostsTheSameNumberOfStatementsForAnyBatchSize()
    {
        var connectionString = Pg.ConnectionString;

        static CandidateIdentity[] Build(int offset, int count) => Enumerable.Range(0, count)
            .Select(index => CandidateIdentity.Create(
                $"{offset:D3}{index:D8}", $"STATEMENT PERSON {offset}-{index}", null))
            .ToArray();

        async Task<int> StatementsForAsync(CandidateIdentity[] batch)
        {
            var counter = new SqlStatementCounter();
            await using var dataSource = counter.BuildDataSource(connectionString);
            var repository = new CandidateRepository(dataSource);

            await repository.EnsureIdentitiesAsync(batch);

            return counter.Count;
        }

        var single = await StatementsForAsync(Build(501, 1));
        var hundred = await StatementsForAsync(Build(502, 100));

        // The number of roundtrips must not scale with the batch: staging, two insert
        // partitions and one read-back.
        hundred.Should().Be(single);

        // CREATE TEMP TABLE, the CPF insert, the CPF-less insert and the read-back. The binary
        // COPY is a replication-style operation and does not raise Npgsql's command event, so it
        // is visible as the absence of an extra statement rather than as one.
        //
        // The previous upsert shape ran one statement fewer (it used RETURNING instead of a
        // read-back) but produced an UPDATE for every conflicting row. Trading one extra
        // statement per batch of arbitrary size for zero updates is the whole point.
        single.Should().Be(4);
    }

    [Fact]
    public async Task BatchEmitsNoUpdateStatement()
    {
        var counter = new SqlStatementCounter();
        await using var dataSource = counter.BuildDataSource(Pg.ConnectionString);
        var repository = new CandidateRepository(dataSource);

        var identities = Enumerable.Range(0, 10)
            .Select(index => CandidateIdentity.Create(
                $"321000{index:D5}", $"NO UPDATE PERSON {index}", null))
            .ToArray();

        await repository.EnsureIdentitiesAsync(identities);

        counter.Writes.Should().NotBeEmpty();
        counter.Writes.Should().OnlyContain(
            statement => statement.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase),
            "identity persistence is insert-only");
    }

    [Fact]
    public async Task CancellationLeavesNoIdentityBehind()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await repository.EnsureIdentitiesAsync(
            [CandidateIdentity.Create("88899900011", "CANCELLED PERSON", null)],
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        (await CountAsync("CandidateIdentities", "\"Cpf\" = '88899900011'", new { })).Should().Be(0);
    }

    [Fact]
    public async Task EmptyBatchDoesNothing()
    {
        var counter = new SqlStatementCounter();
        var repository = CreateRepository(counter);

        var resolution = await repository.EnsureIdentitiesAsync([]);

        resolution.IdsByKey.Should().BeEmpty();
        resolution.Conflicts.Should().BeEmpty();
        counter.Count.Should().Be(0, "an empty batch must not open a connection");
    }

    /// <summary>
    /// Row versions for a whole predicate, ordered by id, so a test can compare the table before
    /// and after a call without needing to know which ids were written.
    /// </summary>
    private async Task<IReadOnlyList<string>> RowVersionsAsync(
        string table,
        string where,
        object parameters)
    {
        await using var command = Db.CreateCommand();
        command.CommandText =
            $"SELECT xmin::text FROM \"{table}\" WHERE {where} ORDER BY \"Id\";";

        foreach (var property in parameters.GetType().GetProperties())
            command.Parameters.AddWithValue(property.Name, property.GetValue(parameters)!);

        var versions = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            versions.Add(reader.GetString(0));

        return versions;
    }
}
