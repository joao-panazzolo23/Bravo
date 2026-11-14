using Bravo.Infrastructure.Acquisition.Queue;
using FluentAssertions;

namespace Bravo.UnitTests.Acquisition;

public class ImportExecutionQueueTests
{
    [Fact]
    public async Task EnqueueAndReadAllAsync_ShouldDeliverItemsInOrder()
    {
        var queue = new ImportExecutionQueue();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await queue.Enqueue(id1);
        await queue.Enqueue(id2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<Guid>();

        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            received.Add(item);
            if (received.Count == 2) break;
        }

        received.Should().ContainInOrder(id1, id2);
    }

    [Fact]
    public void TryCancel_ShouldCancelRegisteredTokenSource()
    {
        var queue = new ImportExecutionQueue();
        var id = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        queue.RegisterActive(id, cts);

        var cancelled = queue.TryCancel(id);

        cancelled.Should().BeTrue();
        cts.IsCancellationRequested.Should().BeTrue();

        queue.UnregisterActive(id);
        var secondAttempt = queue.TryCancel(id);
        secondAttempt.Should().BeFalse();
    }
}

