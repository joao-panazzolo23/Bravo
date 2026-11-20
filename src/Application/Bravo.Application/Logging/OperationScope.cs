using System.Collections;

namespace Bravo.Application.Logging;

internal readonly struct OperationScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly KeyValuePair<string, object?>[] _items;

    public OperationScope(int? year = null, string? state = null, string? workerId = null, string? correlationId = null)
    {
        var count = 0;
        if (year.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(state)) count++;
        if (!string.IsNullOrWhiteSpace(workerId)) count++;
        if (!string.IsNullOrWhiteSpace(correlationId)) count++;

        _items = new KeyValuePair<string, object?>[count];
        var index = 0;
        if (year.HasValue)
            _items[index++] = new KeyValuePair<string, object?>("Year", year.Value);

        if (!string.IsNullOrWhiteSpace(state))
            _items[index++] = new KeyValuePair<string, object?>("State", state);

        if (!string.IsNullOrWhiteSpace(workerId))
            _items[index++] = new KeyValuePair<string, object?>("WorkerId", workerId);

        if (!string.IsNullOrWhiteSpace(correlationId))
            _items[index] = new KeyValuePair<string, object?>("CorrelationId", correlationId);
    }

    public int Count => _items.Length;

    public KeyValuePair<string, object?> this[int index] => _items[index];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        return _items.Length == 0 ? "[]" : string.Join(", ", _items.Select(x => $"{x.Key}: {x.Value}"));
    }
}
