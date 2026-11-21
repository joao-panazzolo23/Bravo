using System.Threading.Channels;
using Bravo.Application.Importing.Services;
using FluentAssertions;

namespace Bravo.UnitTests.Importing;

/// <summary>
/// The bounded pipeline is what keeps a large file from outpacing the database. These tests pin
/// the backpressure, failure-propagation and cancellation behaviour that guarantee makes.
/// </summary>
public sealed class BoundedPipelineTests
{
    [Fact]
    public async Task PersistsEveryBatchInOrder()
    {
        var consumed = new List<int>();

        await BoundedPipeline.RunAsync<int>(
            capacity: 2,
            async (writer, token) =>
            {
                for (var batch = 0; batch < 10; batch++)
                    await writer.WriteAsync(batch, token);
            },
            (batch, token) =>
            {
                consumed.Add(batch);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        consumed.Should().Equal(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task ProducerWaitsWhenTheChannelIsFull()
    {
        var persisted = new int[1];
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var written = 0;
        var producerFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = BoundedPipeline.RunAsync<int>(
            capacity: 1,
            async (writer, token) =>
            {
                for (var batch = 0; batch < 5; batch++)
                {
                    await writer.WriteAsync(batch, token);
                    Interlocked.Increment(ref written);
                }

                producerFinished.TrySetResult();
            },
            async (batch, token) =>
            {
                // Block the only consumer until the test releases it.
                await release.Task;
                Interlocked.Increment(ref persisted[0]);
            },
            CancellationToken.None);

        // The consumer is blocked and the channel holds one item, so the producer can have
        // written at most capacity + in-flight (2) and must now be waiting.
        await Task.Delay(200);

        Volatile.Read(ref written).Should().BeLessThan(5);
        producerFinished.Task.IsCompleted.Should().BeFalse();

        release.TrySetResult();
        await run;

        producerFinished.Task.IsCompleted.Should().BeTrue();
        persisted[0].Should().Be(5);
    }

    [Fact]
    public async Task ConsumerFailureStopsTheProducer()
    {
        var produced = 0;

        var act = () => BoundedPipeline.RunAsync<int>(
            capacity: 2,
            async (writer, token) =>
            {
                for (var batch = 0; batch < 1_000; batch++)
                {
                    await writer.WriteAsync(batch, token);
                    Interlocked.Increment(ref produced);
                }
            },
            (batch, token) => batch == 3
                ? Task.FromException(new InvalidOperationException("database unavailable"))
                : Task.CompletedTask,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // The producer must not have run to completion after the consumer failed.
        Volatile.Read(ref produced).Should().BeLessThan(1_000);
    }

    [Fact]
    public async Task ProducerFailureIsSurfacedAndConsumerStops()
    {
        var consumed = 0;

        var act = () => BoundedPipeline.RunAsync<int>(
            capacity: 2,
            async (writer, token) =>
            {
                await writer.WriteAsync(0, token);
                await writer.WriteAsync(1, token);

                throw new FormatException("bad row");
            },
            (batch, token) =>
            {
                Interlocked.Increment(ref consumed);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<FormatException>();

        Volatile.Read(ref consumed).Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task CancellationPropagatesThroughBothStages()
    {
        using var cts = new CancellationTokenSource();

        var act = () => BoundedPipeline.RunAsync<int>(
            capacity: 1,
            async (writer, token) =>
            {
                for (var batch = 0; batch < 1_000; batch++)
                    await writer.WriteAsync(batch, token);
            },
            async (batch, token) =>
            {
                await Task.Delay(10, token);
            },
            cts.Token);

        var running = act();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [Fact]
    public async Task NoItemIsPersistedTwice()
    {
        var seen = new Dictionary<int, int>();

        await BoundedPipeline.RunAsync<int>(
            capacity: 3,
            async (writer, token) =>
            {
                for (var batch = 0; batch < 50; batch++)
                    await writer.WriteAsync(batch, token);
            },
            (batch, token) =>
            {
                lock (seen)
                    seen[batch] = seen.GetValueOrDefault(batch) + 1;

                return Task.CompletedTask;
            },
            CancellationToken.None);

        seen.Should().HaveCount(50);
        seen.Values.Should().OnlyContain(count => count == 1);
    }

    [Fact]
    public async Task RejectsNonPositiveCapacity()
    {
        // Capacity is validated before any asynchronous work starts.
        Func<Task> act = () => BoundedPipeline.RunAsync<int>(
            capacity: 0,
            (writer, token) => Task.CompletedTask,
            (batch, token) => Task.CompletedTask,
            CancellationToken.None);

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(act);
    }
}
