using System.Threading.Channels;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Runs parsing and persistence concurrently with a hard bound on how much work is in flight.
/// </summary>
/// <remarks>
/// The producer pushes batches into a bounded channel; a single consumer drains it. When the
/// channel is full the producer's write blocks, so a slow database throttles parsing instead of
/// letting it allocate unboundedly. Ownership of each batch transfers to the channel, and the
/// producer allocates no further batch until the consumer has taken one, so at most
/// <c>capacity + 2</c> batches exist at once (the queued ones, the one being consumed and the
/// one being built).
///
/// A failure on either side faulted the channel and cancels the other side, so neither keeps
/// working after the pipeline is broken. Cancellation is propagated through the same token.
/// </remarks>
public static class BoundedPipeline
{
    /// <summary>
    /// Streams items from <paramref name="produce"/> to <paramref name="consume"/> through a
    /// bounded channel, returning when both sides have finished.
    /// </summary>
    /// <param name="produce">
    /// Writes batches with <c>await writer.WriteAsync(batch, token)</c>. Must complete the
    /// writer when finished; <paramref name="completeWriter"/> is provided for that.
    /// </param>
    /// <param name="consume">
    /// Receives each batch in order. Awaited to completion before the next batch is read, which
    /// is what keeps database writes serialized within one scope.
    /// </param>
    public static async Task RunAsync<T>(
        int capacity,
        Func<ChannelWriter<T>, CancellationToken, Task> produce,
        Func<T, CancellationToken, Task> consume,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(consume);

        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            // Wait is the whole point: a full channel must apply backpressure to the producer
            // rather than drop batches or grow without limit.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var producer = ProduceAsync(channel.Writer, produce, linked);
        var consumer = ConsumeAsync(channel.Reader, consume, linked);

        try
        {
            await Task.WhenAll(producer, consumer);
        }
        catch
        {
            // One side failed or was cancelled: stop the other and make sure the writer is
            // closed so a blocked WriteAsync is released instead of hanging shutdown.
            linked.Cancel();
            channel.Writer.TryComplete();

            // Observe both tasks so no exception is lost when the first one threw.
            await Task.WhenAll(
                Swallow(producer),
                Swallow(consumer));

            throw;
        }
    }

    private static async Task ProduceAsync<T>(
        ChannelWriter<T> writer,
        Func<ChannelWriter<T>, CancellationToken, Task> produce,
        CancellationTokenSource linked)
    {
        try
        {
            await produce(writer, linked.Token);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
            linked.Cancel();
            throw;
        }
    }

    private static async Task ConsumeAsync<T>(
        ChannelReader<T> reader,
        Func<T, CancellationToken, Task> consume,
        CancellationTokenSource linked)
    {
        try
        {
            await foreach (var batch in reader.ReadAllAsync(linked.Token))
                await consume(batch, linked.Token);
        }
        catch
        {
            linked.Cancel();
            throw;
        }
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Already surfaced by Task.WhenAll above.
        }
    }
}
