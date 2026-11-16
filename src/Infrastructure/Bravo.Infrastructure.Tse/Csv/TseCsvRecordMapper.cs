using Bravo.Infrastructure.Tse.Layouts;

namespace Bravo.Infrastructure.Tse.Csv;

public sealed record TseRecord(
    IReadOnlyDictionary<string, string> Values);

public sealed class TseLayoutMismatchException(string message) : Exception(message);

/// <summary>
/// Maps raw delimited rows onto canonical field names declared by a <see cref="TseLayout"/>.
/// </summary>
/// <remarks>
/// Source columns absent from the file are skipped; canonical fields the layout declares but
/// the file does not contain are reported so a layout mismatch fails loudly instead of
/// silently producing empty values.
/// </remarks>
public sealed class TseCsvRecordMapper
{
    private readonly TseLayout _layout;
    private readonly string[] _canonicalByIndex;
    private readonly IReadOnlyList<string> _missingColumns;

    private TseCsvRecordMapper(
        TseLayout layout,
        string[] canonicalByIndex,
        IReadOnlyList<string> missingColumns)
    {
        _layout = layout;
        _canonicalByIndex = canonicalByIndex;
        _missingColumns = missingColumns;
    }

    public TseLayout Layout => _layout;

    public IReadOnlyList<string> MissingColumns => _missingColumns;

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

        return new TseCsvRecordMapper(layout, canonicalByIndex, missingRequired);
    }

    public TseRecord Map(IReadOnlyList<string> row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var values = new Dictionary<string, string>(_canonicalByIndex.Length, StringComparer.Ordinal);

        for (var index = 0; index < _canonicalByIndex.Length && index < row.Count; index++)
        {
            var canonical = _canonicalByIndex[index];

            if (canonical.Length == 0)
                continue;

            values[canonical] = row[index];
        }

        return new TseRecord(values);
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
