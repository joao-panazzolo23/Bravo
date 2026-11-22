using Bravo.Application.Imports.Tse.Services;

namespace Bravo.Application.Imports.Tse.Models;

/// <summary>
/// Aggregates TSE result rows into batches of distinct result keys.
/// </summary>
/// <remarks>
/// A results file publishes one row per vote type, ballot and candidate for a section, and several
/// rows can collapse onto the same key (the same ballot counted twice in a file). Summing them
/// before persisting is what keeps the upsert key unique and the vote total correct.
///
/// The buffer is a plain dictionary because it is owned by exactly one producer. When a batch is
/// handed to the bounded pipeline its ownership transfers and a fresh accumulator is created, so
/// the consumer never observes mutation. That is the contract the pipeline relies on.
/// </remarks>
public sealed class TseResultAccumulator(int batchSize)
{
    private readonly Dictionary<TseResultKey, long> _values = new(batchSize);

    public int Count => _values.Count;

    public bool IsFull => _values.Count >= batchSize;

    public IReadOnlyDictionary<TseResultKey, long> Values => _values;

    /// <summary>
    /// Adds the votes of one row to its key. Returns <c>true</c> when the batch is full and the
    /// caller should hand it to persistence.
    /// </summary>
    public bool Add(TseResultKey key, long votes)
    {
        _values[key] = _values.TryGetValue(key, out var existing) ? existing + votes : votes;

        return IsFull;
    }

    /// <summary>
    /// Takes the accumulated values and empties this instance, so the caller can hand the
    /// dictionary to another thread without copying it.
    /// </summary>
    public Dictionary<TseResultKey, long> Take()
    {
        var taken = new Dictionary<TseResultKey, long>(_values.Count);

        foreach (var entry in _values)
            taken[entry.Key] = entry.Value;

        _values.Clear();

        return taken;
    }
}
