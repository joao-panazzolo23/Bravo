using System.Runtime.CompilerServices;

namespace Bravo.Application.Acquisition.Services;

/// <summary>
/// Groups a stream into bounded batches without materializing the whole dataset.
/// The last partial batch is always flushed.
/// </summary>
public static class BatchStream
{
    public static async IAsyncEnumerable<IReadOnlyList<T>> Chunk<T>(
        IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var buffer = new List<T>(batchSize);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            buffer.Add(item);
            if (buffer.Count >= batchSize)
            {
                yield return buffer;
                buffer = new List<T>(batchSize);
            }
        }

        if (buffer.Count > 0)
        {
            yield return buffer;
        }
    }
}
