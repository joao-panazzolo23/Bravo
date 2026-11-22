namespace Bravo.Infrastructure.Tse.Csv;

/// <summary>
/// One parsed source row, exposed by canonical field name.
/// </summary>
/// <remarks>
/// Reading a results file means creating tens of millions of these, and every allocation in the
/// hot path costs both time and GC pressure. The row therefore keeps the parsed field array and
/// resolves canonical names through the mapper's index table instead of building a dictionary
/// per row. <see cref="Values"/> still exists for callers that prefer the readable shape, but it
/// is a lazy view: the hot path never touches it.
/// </remarks>
public sealed class TseRecord(
    IReadOnlyList<string> fields,
    IReadOnlyDictionary<string, int> canonicalToIndex)
{
    private Dictionary<string, string>? _values;

    public IReadOnlyList<string> Fields { get; } = fields;

    /// <summary>Raw field by canonical name. Returns null when the file omits the column.</summary>
    public string? GetValue(string canonical)
        => canonicalToIndex.TryGetValue(canonical, out var index) && index < Fields.Count
            ? Fields[index]
            : null;

    /// <summary>Raw field by canonical name; throws when the column is missing.</summary>
    public string this[string canonical]
        => GetValue(canonical)
           ?? throw new KeyNotFoundException($"Column '{canonical}' is not present in the file.");

    public IReadOnlyDictionary<string, string> Values
        => _values ??= BuildValues();

    private Dictionary<string, string> BuildValues()
    {
        var values = new Dictionary<string, string>(canonicalToIndex.Count, StringComparer.Ordinal);

        foreach (var (canonical, index) in canonicalToIndex)
        {
            if (index < Fields.Count)
                values[canonical] = Fields[index];
        }

        return values;
    }
}

public sealed class TseLayoutMismatchException(string message) : Exception(message);
