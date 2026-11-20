using System.Collections;

namespace Bravo.Application.Logging;

internal readonly struct JobScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly KeyValuePair<string, object?>[] _items;

    public JobScope(Guid jobId, string source, string? workerId = null, string? correlationId = null)
    {
        var count = 2;
        if (!string.IsNullOrWhiteSpace(workerId)) count++;
        if (!string.IsNullOrWhiteSpace(correlationId)) count++;

        _items = new KeyValuePair<string, object?>[count];
        _items[0] = new KeyValuePair<string, object?>("JobId", jobId);
        _items[1] = new KeyValuePair<string, object?>("Source", source);

        var index = 2;
        if (!string.IsNullOrWhiteSpace(workerId))
            _items[index++] = new KeyValuePair<string, object?>("WorkerId", workerId);

        if (!string.IsNullOrWhiteSpace(correlationId))
            _items[index] = new KeyValuePair<string, object?>("CorrelationId", correlationId);
    }

    public int Count => _items.Length;

    public KeyValuePair<string, object?> this[int index] => _items[index];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"[JobId: {_items[0].Value}, Source: {_items[1].Value}]";
}
