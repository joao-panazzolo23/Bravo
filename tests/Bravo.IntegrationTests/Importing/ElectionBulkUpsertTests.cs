using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using Bravo.IntegrationTests.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Covers the guarded upsert of elections and rounds.
/// </summary>
/// <remarks>
/// The reference stage re-reads the same elections on every year of every job, so the point of
/// these tests is that an unchanged row is not rewritten, while a genuine correction still applies
/// and the caller always receives the stored row. Only a real database can prove both: the write
/// is observed through <c>xmin</c>, and the identifier recovery depends on PostgreSQL's
/// <c>RETURNING</c> semantics.
/// </remarks>
[Collection("Database")]
public sealed class ElectionBulkUpsertTests(PostgresContainerFixture pg) : BulkPersistenceTestBase(pg)
{
    private IElectionRepository Repository(IServiceProvider services)
        => services.GetRequiredService<IElectionRepository>();

    private static Election Build(
        int code = 9200,
        string description = "ELEIÇÃO GERAL",
        DateOnly? date = null) => Election.Create(
        code,
        2022,
        1,
        "ORDINARIA",
        description,
        date ?? new DateOnly(2022, 10, 2));

    [Fact]
    public async Task FirstInsertionReturnsTheNewRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var election = Build();

        var stored = await repository.UpsertAsync(election);

        stored.Id.Should().Be(election.Id);
        stored.TseElectionCode.Should().Be(9200);
        stored.Description.Should().Be("ELEIÇÃO GERAL");
        (await CountAsync("Elections", "\"TseElectionCode\" = 9200", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ReimportingAnUnchangedElectionDoesNotRewriteTheRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var first = await repository.UpsertAsync(Build());
        var versionBefore = await RowVersionAsync("Elections", first.Id);
        var updatedAtBefore = await ScalarAsync<DateTime>(
            """SELECT "UpdatedAt" FROM "Elections" WHERE "Id" = @id;""", new { id = first.Id });

        // A fresh entity instance, as the next import would build it.
        var second = await repository.UpsertAsync(Build());

        second.Id.Should().Be(first.Id, "the stored identity must be preserved");

        (await RowVersionAsync("Elections", first.Id)).Should().Be(versionBefore);
        (await ScalarAsync<DateTime>(
            """SELECT "UpdatedAt" FROM "Elections" WHERE "Id" = @id;""", new { id = first.Id }))
            .Should().Be(updatedAtBefore);
        (await CountAsync("Elections", "\"TseElectionCode\" = 9200", new { })).Should().Be(1);
    }

    [Fact]
    public async Task AChangedElectionIsUpdatedAndKeepsItsIdentity()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var first = await repository.UpsertAsync(Build(description: "ORIGINAL"));
        var versionBefore = await RowVersionAsync("Elections", first.Id);

        var second = await repository.UpsertAsync(Build(description: "CORRIGIDA"));

        second.Id.Should().Be(first.Id);
        second.Description.Should().Be("CORRIGIDA");

        (await RowVersionAsync("Elections", first.Id)).Should().NotBe(versionBefore);
        (await ScalarAsync<string>(
            """SELECT "Description" FROM "Elections" WHERE "Id" = @id;""", new { id = first.Id }))
            .Should().Be("CORRIGIDA");
    }

    [Fact]
    public async Task FirstRoundInsertionReturnsTheNewRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var election = await repository.UpsertAsync(Build());

        var round = await repository.UpsertRoundAsync(
            ElectionRound.Create(election.Id, 1));

        round.ElectionId.Should().Be(election.Id);
        round.Round.Should().Be(1);
        round.IsRunoff.Should().BeFalse();
        (await CountAsync("ElectionRounds", "\"ElectionId\" = @id", new { id = election.Id }))
            .Should().Be(1);
    }

    [Fact]
    public async Task ReimportingAnUnchangedRoundDoesNotRewriteTheRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var election = await repository.UpsertAsync(Build());
        var first = await repository.UpsertRoundAsync(ElectionRound.Create(election.Id, 1));

        var versionBefore = await RowVersionAsync("ElectionRounds", first.Id);

        var second = await repository.UpsertRoundAsync(ElectionRound.Create(election.Id, 1));

        second.Id.Should().Be(first.Id);
        (await RowVersionAsync("ElectionRounds", first.Id)).Should().Be(versionBefore);
    }

    [Fact]
    public async Task ARunoffRoundIsFlaggedAndKeepsItsIdentity()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var election = await repository.UpsertAsync(Build());
        var first = await repository.UpsertRoundAsync(ElectionRound.Create(election.Id, 2));

        var second = await repository.UpsertRoundAsync(ElectionRound.Create(election.Id, 2));

        second.Id.Should().Be(first.Id);
        second.Round.Should().Be(2);
        second.IsRunoff.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentInsertionOfTheSameElectionConvergesOnOneRow()
    {
        // Several year-level reference stages racing for the same election code.
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var scope = Pg.Factory.Services.CreateAsyncScope();
            var repository = Repository(scope.ServiceProvider);

            return await repository.UpsertAsync(Build(code: 9300));
        })));

        results.Select(election => election.Id).Distinct().Should().ContainSingle();
        (await CountAsync("Elections", "\"TseElectionCode\" = 9300", new { })).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentInsertionOfTheSameRoundConvergesOnOneRow()
    {
        await using (var seedScope = Pg.Factory.Services.CreateAsyncScope())
        {
            var seedRepository = Repository(seedScope.ServiceProvider);
            await seedRepository.UpsertAsync(Build(code: 9400));
        }

        var electionId = await ScalarAsync<Guid>(
            """SELECT "Id" FROM "Elections" WHERE "TseElectionCode" = 9400;""");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var scope = Pg.Factory.Services.CreateAsyncScope();
            var repository = Repository(scope.ServiceProvider);

            return await repository.UpsertRoundAsync(ElectionRound.Create(electionId, 1));
        })));

        results.Select(round => round.Id).Distinct().Should().ContainSingle();
        (await CountAsync("ElectionRounds", "\"ElectionId\" = @id", new { id = electionId }))
            .Should().Be(1);
    }

    [Fact]
    public async Task ReimportIsIdempotentAcrossEveryRow()
    {
        await using var scope = Pg.Factory.Services.CreateAsyncScope();
        var repository = Repository(scope.ServiceProvider);

        var elections = new[] { Build(9500), Build(9501), Build(9502) };

        foreach (var election in elections)
        {
            var stored = await repository.UpsertAsync(election);
            await repository.UpsertRoundAsync(ElectionRound.Create(stored.Id, 1));
        }

        var electionVersions = await ScalarAsync<string>(
            """SELECT string_agg(xmin::text, ',' ORDER BY "Id") FROM "Elections" WHERE "TseElectionCode" BETWEEN 9500 AND 9502;""");
        var roundVersions = await ScalarAsync<string>(
            """SELECT string_agg(r.xmin::text, ',' ORDER BY r."Id") FROM "ElectionRounds" AS r INNER JOIN "Elections" AS e ON e."Id" = r."ElectionId" WHERE e."TseElectionCode" BETWEEN 9500 AND 9502;""");

        foreach (var election in elections)
        {
            var stored = await repository.UpsertAsync(election);
            await repository.UpsertRoundAsync(ElectionRound.Create(stored.Id, 1));
        }

        (await ScalarAsync<string>(
            """SELECT string_agg(xmin::text, ',' ORDER BY "Id") FROM "Elections" WHERE "TseElectionCode" BETWEEN 9500 AND 9502;"""))
            .Should().Be(electionVersions);

        (await ScalarAsync<string>(
            """SELECT string_agg(r.xmin::text, ',' ORDER BY r."Id") FROM "ElectionRounds" AS r INNER JOIN "Elections" AS e ON e."Id" = r."ElectionId" WHERE e."TseElectionCode" BETWEEN 9500 AND 9502;"""))
            .Should().Be(roundVersions);

        (await CountAsync("Elections", "\"TseElectionCode\" BETWEEN 9500 AND 9502", new { }))
            .Should().Be(3);
    }
}
