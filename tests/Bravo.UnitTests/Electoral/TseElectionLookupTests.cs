using Bravo.Application.Imports.Tse;
using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Entities;
using Bravo.Domain.Electoral.Repositories;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Covers the job-scoped election snapshot that replaced the per-record lookup. It must resolve
/// exactly the codes it was built from, and must never invent an election.
/// </summary>
public sealed class TseElectionLookupTests
{
    [Fact]
    public async Task LoadsEveryElectionAndRound()
    {
        var first = Election.Create(544, 2022, 1, "ORDINARIA", "2022", null);
        var second = Election.Create(545, 2022, 1, "ORDINARIA", "2022", null);

        var lookup = await TseElectionLookup.LoadAsync(new FakeElectionRepository(
            [first, second],
            [ElectionRound.Create(first.Id, 1),
             ElectionRound.Create(second.Id, 2)]));

        lookup.TryGetElectionId(544, out var resolvedFirst).Should().BeTrue();
        lookup.TryGetElectionId(545, out var resolvedSecond).Should().BeTrue();
        resolvedFirst.Should().Be(first.Id);
        resolvedSecond.Should().Be(second.Id);

        lookup.TryGetRoundId(544, 1, out var firstRound).Should().BeTrue();
        lookup.TryGetRoundId(545, 2, out var secondRound).Should().BeTrue();
        firstRound.Should().NotBe(secondRound);
    }

    [Fact]
    public async Task MissingElectionResolvesToFalse()
    {
        var lookup = await TseElectionLookup.LoadAsync(new FakeElectionRepository([], []));

        lookup.TryGetElectionId(9999, out _).Should().BeFalse();
        lookup.TryGetRoundId(9999, 1, out _).Should().BeFalse();
    }

    [Fact]
    public async Task RoundsAreMatchedToTheirElectionCode()
    {
        var election = Election.Create(544, 2022, 1, "ORDINARIA", "2022", null);
        var runoff = ElectionRound.Create(Guid.CreateVersion7(), 2);

        var lookup = await TseElectionLookup.LoadAsync(new FakeElectionRepository(
            [election],
            [ElectionRound.Create(election.Id, 1), runoff]));

        lookup.TryGetRoundId(544, 1, out var firstRound).Should().BeTrue();
        firstRound.Should().NotBe(runoff.Id);

        // A round whose election is absent from the snapshot must not resolve, otherwise a
        // result could be attributed to a round the import never created.
        lookup.TryGetRoundId(544, 2, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentReadersSeeConsistentResults()
    {
        var election = Election.Create(544, 2022, 1, "ORDINARIA", "2022", null);
        var round = ElectionRound.Create(election.Id, 1);

        var lookup = await TseElectionLookup.LoadAsync(
            new FakeElectionRepository([election], [round]));

        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(async _ =>
        {
            await Task.Yield();

            lookup.TryGetElectionId(544, out var electionId).Should().BeTrue();
            lookup.TryGetRoundId(544, 1, out var roundId).Should().BeTrue();

            return (ElectionId: electionId, RoundId: roundId);
        }));

        results.Should().OnlyContain(result =>
            result.ElectionId == election.Id && result.RoundId == round.Id);
    }

    private sealed class FakeElectionRepository(
        IReadOnlyList<Election> elections,
        IReadOnlyList<ElectionRound> rounds) : IElectionRepository
    {
        public Task<Election> UpsertAsync(Election election, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ElectionRound> UpsertRoundAsync(
            ElectionRound round, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Election>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(elections);

        public Task<IReadOnlyList<ElectionRound>> ListRoundsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(rounds);
    }
}
