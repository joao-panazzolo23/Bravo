using Bravo.Infrastructure.Tse.Layouts;

namespace Bravo.Infrastructure.Tse.Csv;

/// <summary>
/// Maps raw delimited rows onto canonical field names declared by a <see cref="TseLayout"/>.
/// </summary>
/// <remarks>
/// Source columns absent from the file are skipped; canonical fields the layout declares but
/// the file does not contain are reported so a layout mismatch fails loudly instead of
/// silently producing empty values.
/// </remarks>
internal sealed class TseCsvRecordMapper
{
    private readonly TseLayout _layout;
    private readonly string[] _canonicalByIndex;
    private readonly IReadOnlyDictionary<string, int> _indexByCanonical;
    private readonly IReadOnlyList<string> _missingColumns;

    private TseCsvRecordMapper(
        TseLayout layout,
        string[] canonicalByIndex,
        IReadOnlyDictionary<string, int> indexByCanonical,
        IReadOnlyList<string> missingColumns)
    {
        _layout = layout;
        _canonicalByIndex = canonicalByIndex;
        _indexByCanonical = indexByCanonical;
        _missingColumns = missingColumns;
    }

    // public TseLayout Layout => _layout;

    // public IReadOnlyList<string> MissingColumns => _missingColumns;

    public static TseCsvRecordMapper Create(
        TseLayout layout,
        IReadOnlyList<string> header)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(header);

        var canonicalByIndex = new string[header.Count];
        var present = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < header.Count; index++)
        {
            var sourceColumn = Normalise(header[index]);

            if (layout.Columns.TryGetValue(sourceColumn, out var canonical))
            {
                canonicalByIndex[index] = canonical;
                present.Add(canonical);
            }
            else
            {
                canonicalByIndex[index] = string.Empty;
            }
        }

        var missing = layout.Columns
            .Where(column => !present.Contains(column.Value))
            .Select(column => column.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var missingRequired = missing
            .Where(sourceColumn => layout.RequiredCanonicalFields.Contains(layout.Columns[sourceColumn]))
            .ToArray();

        // Built once per file: the hot path resolves a canonical name through this map instead
        // of constructing a dictionary for every row.
        var indexByCanonical = new Dictionary<string, int>(present.Count, StringComparer.Ordinal);

        for (var index = 0; index < canonicalByIndex.Length; index++)
        {
            var canonical = canonicalByIndex[index];

            if (canonical.Length > 0 && !indexByCanonical.ContainsKey(canonical))
                indexByCanonical[canonical] = index;
        }

        return new TseCsvRecordMapper(
            layout, canonicalByIndex, indexByCanonical, missingRequired);
    }

    public TseRecord Map(IReadOnlyList<string> row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new TseRecord(row, _indexByCanonical);
    }

    public void ThrowIfLayoutIncomplete()
    {
        if (_missingColumns.Count == 0)
            return;

        throw new TseLayoutMismatchException(
            $"Layout '{_layout.Version}' expects columns that are missing from the file: " +
            string.Join(", ", _missingColumns) + ".");
    }

    private static string Normalise(string column)
        => column.Trim().TrimStart('\uFEFF').ToUpperInvariant();
}
