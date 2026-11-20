using System.Collections;

namespace Bravo.Application.Logging;

internal readonly struct WorkUnitScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly KeyValuePair<string, object?>[] _items;

    public WorkUnitScope(Guid workUnitId, int? year = null, string? state = null, string? correlationId = null)
    {
        var count = 1;
        if (year.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(state)) count++;
        if (!string.IsNullOrWhiteSpace(correlationId)) count++;

        _items = new KeyValuePair<string, object?>[count];
        _items[0] = new KeyValuePair<string, object?>("WorkUnitId", workUnitId);

        var index = 1;
        if (year.HasValue)
            _items[index++] = new KeyValuePair<string, object?>("Year", year.Value);

        if (!string.IsNullOrWhiteSpace(state))
            _items[index++] = new KeyValuePair<string, object?>("State", state);

        if (!string.IsNullOrWhiteSpace(correlationId))
            _items[index] = new KeyValuePair<string, object?>("CorrelationId", correlationId);
    }

    public int Count => _items.Length;

    public KeyValuePair<string, object?> this[int index] => _items[index];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"[WorkUnitId: {_items[0].Value}]";
}
