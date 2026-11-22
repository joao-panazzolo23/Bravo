using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Models;
using Bravo.Domain.Electoral.Enums;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

/// <summary>
/// Pins the batching contract the streaming result pipeline relies on.
/// </summary>
public sealed class TseResultAccumulatorTests
{
    private static TseResultKey Key(int ballot = 1) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        VoteType.Nominal,
        Guid.CreateVersion7(),
        null,
        ballot);

    [Fact]
    public void VotesForTheSameKeyAreSummed()
    {
        var accumulator = new TseResultAccumulator(batchSize: 10);
        var key = Key();

        accumulator.Add(key, 5);
        accumulator.Add(key, 7);

        // TSE can publish the same ballot twice in one file; the result must be the total, not
        // the last value.
        accumulator.Values[key].Should().Be(12);
        accumulator.Count.Should().Be(1);
    }

    [Fact]
    public void DistinctKeysAreCountedSeparately()
    {
        var accumulator = new TseResultAccumulator(batchSize: 10);

        accumulator.Add(Key(1), 1);
        accumulator.Add(Key(2), 2);

        accumulator.Count.Should().Be(2);
        accumulator.Values.Values.Should().BeEquivalentTo([1L, 2L]);
    }

    [Fact]
    public void BecomingFullIsReportedSoTheCallerCanFlush()
    {
        var accumulator = new TseResultAccumulator(batchSize: 2);

        accumulator.Add(Key(1), 1).Should().BeFalse();
        accumulator.Add(Key(2), 1).Should().BeTrue("the batch reached its configured size");
        accumulator.IsFull.Should().BeTrue();
    }

    [Fact]
    public void TakeReturnsTheValuesAndEmptiesTheAccumulator()
    {
        var accumulator = new TseResultAccumulator(batchSize: 10);
        var key = Key();

        accumulator.Add(key, 3);

        var taken = accumulator.Take();

        taken[key].Should().Be(3);
        accumulator.Count.Should().Be(0, "ownership moved to the consumer");
        accumulator.Values.Should().BeEmpty();
    }

    [Fact]
    public void TakeProducesAnIndependentDictionary()
    {
        var accumulator = new TseResultAccumulator(batchSize: 10);
        var key = Key();

        accumulator.Add(key, 3);

        var taken = accumulator.Take();

        // Adding to the accumulator again must not mutate the batch that is already in flight.
        accumulator.Add(key, 9);

        taken[key].Should().Be(3);
        accumulator.Values[key].Should().Be(9);
    }
}
