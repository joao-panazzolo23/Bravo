using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Bravo.Domain.Electoral.Services;
using Npgsql;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Binary COPY coverage for candidate identities and candidate registrations.
/// </summary>
[Collection("Database")]
public sealed class CandidateBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    [Fact]
    public async Task IdentitiesAreInsertedWithCorrectValuesAndTypes()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var identity = CandidateIdentity.Create("123.456.789-01", "MARIA DA SILVA", new DateOnly(1980, 5, 4));

        var ids = (await repository.EnsureIdentitiesAsync([identity])).IdsByKey;

        ids.Should().ContainKey(CandidateIdentityKey.Of(identity.Cpf, identity.FullName, identity.BirthDate));
        ids.Values.Single().Should().Be(identity.Id);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "Cpf", "FullName", "BirthDate" FROM "CandidateIdentities" WHERE "Id" = @id;
            """;
        command.Parameters.AddWithValue("id", identity.Id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        // CPF must arrive normalised to eleven digits and the date must survive the binary date
        // encoding, not just round-trip through the same CLR type that produced it.
        reader.GetString(0).Should().Be("12345678901");
        reader.GetString(1).Should().Be("MARIA DA SILVA");
        reader.GetFieldValue<DateOnly>(2).Should().Be(new DateOnly(1980, 5, 4));
    }

    [Fact]
    public async Task IdentityWithoutCpfUsesThePartialUniqueIndexAndReturnsTheExistingId()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var first = CandidateIdentity.Create(null, "JOSE SEM CPF", null);
        var second = CandidateIdentity.Create(null, "JOSE SEM CPF", null);

        var firstIds = (await repository.EnsureIdentitiesAsync([first])).IdsByKey;
        var secondIds = (await repository.EnsureIdentitiesAsync([second])).IdsByKey;

        firstIds.Values.Single().Should().Be(first.Id);
        secondIds.Values.Single().Should().Be(first.Id, "the conflict must resolve to the stored row");

        (await CountAsync("CandidateIdentities", "\"FullName\" = 'JOSE SEM CPF'", new { }))
            .Should().Be(1);
    }

    [Fact]
    public async Task MixedCpfAndCpfLessIdentitiesInOneBatchBothPersist()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var withCpf = CandidateIdentity.Create("11122233344", "WITH CPF", new DateOnly(1970, 1, 1));
        var withoutCpf = CandidateIdentity.Create(null, "WITHOUT CPF", new DateOnly(1971, 2, 2));

        var ids = (await repository.EnsureIdentitiesAsync([withCpf, withoutCpf])).IdsByKey;

        ids.Should().HaveCount(2);
        ids[Constants.WithCpfKey].Should().Be(withCpf.Id);
        ids[Constants.WithoutCpfKey].Should().Be(withoutCpf.Id);

        (await CountAsync("CandidateIdentities", "TRUE", new { })).Should().Be(2);
    }

    [Fact]
    public async Task DuplicateIdentitiesInOneBatchCollapseDeterministically()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var first = CandidateIdentity.Create("55566677788", "DUPLICATED", new DateOnly(1990, 3, 3));
        var second = CandidateIdentity.Create("55566677788", "DUPLICATED", new DateOnly(1990, 3, 3));

        var ids = (await repository.EnsureIdentitiesAsync([first, second])).IdsByKey;

        ids.Should().HaveCount(1);
        (await CountAsync("CandidateIdentities", "\"Cpf\" = '55566677788'", new { })).Should().Be(1);

        // The last staged row wins, which is the accumulation order the import relies on.
        (await ScalarAsync<Guid>(
            """SELECT "Id" FROM "CandidateIdentities" WHERE "Cpf" = '55566677788';"""))
            .Should().Be(second.Id);
    }

    [Fact]
    public async Task CandidatesAreInsertedAndThenUpdatedWithoutChangingTheConflictKey()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        Candidate Build(string urnName, string? socialName) => Candidate.Create(
            seed.ElectionId,
            seed.OfficeId,
            seed.StateId,
            null,
            "SEQ-1",
            13,
            "FULL NAME",
            urnName,
            socialName,
            seed.PartyId,
            null,
            null,
            "DEFERIDO",
            "SUPERIOR COMPLETO",
            "ADVOGADA"
        );

        await repository.UpsertCandidatesAsync([Build("URNA", null)]);
        await repository.UpsertCandidatesAsync([Build("URNA NOVA", "SOCIAL")]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "UrnName", "SocialName", "BallotNumber", "TseCandidateSequential"
            FROM "Candidates" WHERE "ElectionId" = @election AND "StateId" = @state;
            """;
        command.Parameters.AddWithValue("election", seed.ElectionId);
        command.Parameters.AddWithValue("state", seed.StateId);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        reader.GetString(0).Should().Be("URNA NOVA");
        reader.GetString(1).Should().Be("SOCIAL");
        reader.GetInt32(2).Should().Be(13);
        reader.GetString(3).Should().Be("SEQ-1");

        reader.Read().Should().BeFalse("the conflict key must stay unique");
    }

    [Fact]
    public async Task NullableCandidateColumnsRoundTripAsNull()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var candidate = Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-NULL", 1, "ONLY REQUIRED",
            "ONLY REQUIRED", null, null, null, null, null, null, null);

        await repository.UpsertCandidatesAsync([candidate]);

        await using var command = Db.CreateCommand();
        command.CommandText = """
            SELECT "SocialName", "PartyId", "FederationId", "CoalitionId",
                   "RegistrationStatus"
            FROM "Candidates" WHERE "Id" = @id;
            """;
        command.Parameters.AddWithValue("id", candidate.Id);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        for (var index = 0; index < 5; index++)
            reader.IsDBNull(index).Should().BeTrue($"column {index} must stay null");
    }

    [Fact]
    public async Task RepeatingAnUnchangedCandidateBatchDoesNotRewriteTheRow()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        Candidate Build() => Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-NOOP", 7, "UNCHANGED",
            "UNCHANGED", null, seed.PartyId, null, null, "DEFERIDO", null, null);

        var candidate = Build();
        await repository.UpsertCandidatesAsync([candidate]);

        var before = await RowVersionAsync("Candidates", candidate.Id);
        var timestampBefore = await ScalarAsync<DateTime>(
            """SELECT "UpdatedAt" FROM "Candidates" WHERE "Id" = @id;""", new { id = candidate.Id });

        await repository.UpsertCandidatesAsync([Build()]);

        (await RowVersionAsync("Candidates", candidate.Id)).Should().Be(before);

        var timestampAfter = await ScalarAsync<DateTime>(
            """SELECT "UpdatedAt" FROM "Candidates" WHERE "Id" = @id;""", new { id = candidate.Id });

        timestampAfter.Should().Be(timestampBefore);
    }

    [Fact]
    public async Task ChangingOneCandidateColumnStillUpdatesTheRow()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        Candidate Build(string? socialName) => Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-CHANGE", 9, "CHANGES",
            "CHANGES", socialName, seed.PartyId, null, null, "DEFERIDO", null, null);

        var candidate = Build(null);
        await repository.UpsertCandidatesAsync([candidate]);

        var before = await RowVersionAsync("Candidates", candidate.Id);

        await repository.UpsertCandidatesAsync([Build("NOVO NOME SOCIAL")]);

        (await RowVersionAsync("Candidates", candidate.Id)).Should().NotBe(before);
        (await ScalarAsync<string?>(
            """SELECT "SocialName" FROM "Candidates" WHERE "Id" = @id;""", new { id = candidate.Id }))
            .Should().Be("NOVO NOME SOCIAL");
    }

    [Fact]
    public async Task DuplicateCandidateKeysInOneBatchAreCollapsedAndLastWins()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        Candidate Build(string urnName) => Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-DUP", 3, "DUPLICATE",
            urnName, null, seed.PartyId, null, null, null, null, null);

        // The same conflict key twice used to make PostgreSQL reject the whole statement.
        await repository.UpsertCandidatesAsync([Build("FIRST"), Build("SECOND")]);

        (await CountAsync("Candidates", "\"TseCandidateSequential\" = 'SEQ-DUP'", new { }))
            .Should().Be(1);

        (await ScalarAsync<string>(
            """SELECT "UrnName" FROM "Candidates" WHERE "TseCandidateSequential" = 'SEQ-DUP';"""))
            .Should().Be("SECOND");
    }

    [Fact]
    public async Task ForeignKeyViolationsStillAbortTheWholeBatch()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var valid = Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-VALID", 1, "VALID",
            "VALID", null, null, null, null, null, null, null);

        var invalid = Candidate.Create(
            seed.ElectionId, seed.OfficeId, Guid.CreateVersion7(), null, "SEQ-INVALID", 1, "INVALID",
            "INVALID", null, null, null, null, null, null, null);

        var action = async () => await repository.UpsertCandidatesAsync([valid, invalid]);

        await action.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation);

        // Atomicity: the good row of a failed batch must not be committed.
        (await CountAsync("Candidates", "\"TseCandidateSequential\" = 'SEQ-VALID'", new { }))
            .Should().Be(0);
    }

    [Fact]
    public async Task OversizedCandidateValuesSurfaceTheDatabaseErrorInsteadOfTruncating()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        var tooLong = Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-LONG", 1,
            new string('X', 201), new string('X', 201), null, null, null, null,
            null, null, null);

        var action = async () => await repository.UpsertCandidatesAsync([tooLong]);

        await action.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.StringDataRightTruncation);
    }

    [Fact]
    public async Task EmptyBatchesDoNotOpenAConnection()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        (await repository.EnsureIdentitiesAsync([])).IdsByKey.Should().BeEmpty();
        (await repository.UpsertCandidatesAsync([])).Should().Be(0);
    }

    [Fact]
    public async Task CancellationRollsBackTheBatch()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var candidate = Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, "SEQ-CANCEL", 1, "CANCELLED",
            "CANCELLED", null, null, null, null, null, null, null);

        var action = async () => await repository.UpsertCandidatesAsync([candidate], cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        (await CountAsync("Candidates", "\"TseCandidateSequential\" = 'SEQ-CANCEL'", new { }))
            .Should().Be(0);
    }

    [Fact]
    public async Task StagingTableDoesNotLeakBetweenCalls()
    {
        var seed = await SeedReferenceAsync();

        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        Candidate Build(string sequential) => Candidate.Create(
            seed.ElectionId, seed.OfficeId, seed.StateId, null, sequential, 1, sequential,
            sequential, null, null, null, null, null, null, null);

        await repository.UpsertCandidatesAsync([Build("LEAK-1")]);
        await repository.UpsertCandidatesAsync([Build("LEAK-2")]);

        (await CountAsync("Candidates", "\"TseCandidateSequential\" LIKE 'LEAK-%'", new { }))
            .Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentImportsConvergeOnOneRowPerIdentity()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository<ICandidateRepository>(scope.ServiceProvider);

        // Different batches, same natural key: the CPF unique index must arbitrate.
        var identities = Enumerable.Range(0, 8)
            .Select(index => CandidateIdentity.Create(
                "99988877766", "CONCURRENT PERSON", new DateOnly(1990, 1, 1)))
            .ToArray();

        var results = await Task.WhenAll(identities.Select(identity =>
            repository.EnsureIdentitiesAsync([identity])));

        results.Should().OnlyContain(resolution =>
            resolution.IdsByKey.Values.Single() == results[0].IdsByKey.Values.Single());
        (await CountAsync("CandidateIdentities", "\"Cpf\" = '99988877766'", new { })).Should().Be(1);
    }

    private static class Constants
    {
        public static readonly string WithCpfKey =
            CandidateIdentityKey.Of("11122233344", "WITH CPF", new DateOnly(1970, 1, 1));

        public static readonly string WithoutCpfKey =
            CandidateIdentityKey.Of(null, "WITHOUT CPF", new DateOnly(1971, 2, 2));
    }
}
