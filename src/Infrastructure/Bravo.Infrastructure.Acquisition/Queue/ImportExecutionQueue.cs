using System.Collections.Concurrent;
using System.Threading.Channels;
using Bravo.Application.Acquisition.Services;

namespace Bravo.Infrastructure.Acquisition.Queue;

public sealed class ImportExecutionQueue : IImportExecutionQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        }
    );

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeExecutions = new();

    public ValueTask Enqueue(Guid executionId, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(executionId, ct);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public void RegisterActive(Guid executionId, CancellationTokenSource cts)
    {
        _activeExecutions[executionId] = cts;
    }

    public void UnregisterActive(Guid executionId)
    {
        _activeExecutions.TryRemove(executionId, out _);
    }

    public bool TryCancel(Guid executionId)
    {
        if (_activeExecutions.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }
}

