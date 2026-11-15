using System.Globalization;
using System.Text;

namespace Bravo.Infrastructure.Acquisition.Parsing;

/// <summary>
/// Streaming reader for delimited source files (TSE open-data CSV and the FGV crosswalk).
///
/// Column names are the access contract: column ordering and the presence of extra columns
/// may change between election years, so values are always read by name and a missing
/// required column fails fast instead of silently shifting values.
///
/// Delimiter, quote character and encoding are detected from the header, because TSE files
/// are Latin-1 with semicolons and quotes while FGV auxiliary tables are UTF-8 with commas.
/// </summary>
public sealed class DelimitedFileReader : IAsyncDisposable
{
    public static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly StreamReader _reader;
    private readonly string _sourceName;
    private readonly char _delimiter;
    private readonly char _quote;
    private char[] _buffer = new char[4096];
    private int _bufferLength;
    private int _bufferPosition;
    private bool _endOfStream;
    private long _lineNumber;

    private DelimitedFileReader(
        StreamReader reader,
        string sourceName,
        IReadOnlyDictionary<string, int> columns,
        char delimiter,
        char quote)
    {
        _reader = reader;
        _sourceName = sourceName;
        Columns = columns;
        _delimiter = delimiter;
        _quote = quote;
    }

    public IReadOnlyDictionary<string, int> Columns { get; }

    public long LineNumber => _lineNumber;

    public string SourceName => _sourceName;

    public char Delimiter => _delimiter;

    /// <summary>
    /// Opens the reader, detects the layout and reads the header row.
    /// </summary>
    public static async Task<DelimitedFileReader> OpenAsync(
        Stream stream,
        string sourceName,
        IReadOnlyCollection<string> requiredColumns,
        Encoding? encoding = null,
        CancellationToken ct = default)
    {
        var reader = new StreamReader(
            stream,
            encoding ?? Latin1,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 16384,
            leaveOpen: false);

        var first = await reader.ReadLineAsync(ct);
        if (first is null)
        {
            throw new InvalidDataException($"'{sourceName}' is empty.");
        }

        first = first.TrimStart('\uFEFF');
        var delimiter = DetectDelimiter(first);
        var quote = first.Contains('"') ? '"' : '\0';

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var header = SplitLine(first, delimiter, quote);
        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim().Trim(quote).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            columns.TryAdd(name, i);
        }

        var missing = requiredColumns.Where(c => !columns.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException(
                $"'{sourceName}' is missing required columns: {string.Join(", ", missing)}.");
        }

        return new DelimitedFileReader(reader, sourceName, columns, delimiter, quote);
    }

    public bool Has(string column) => Columns.ContainsKey(column);

    /// <summary>
    /// Reads the next row. Returns null at the end of the file. Blank lines are skipped.
    /// </summary>
    public async Task<CsvRow?> ReadAsync(CancellationToken ct)
    {
        while (true)
        {
            var line = await ReadLineAsync(ct);
            if (line is null)
            {
                return null;
            }

            _lineNumber++;

            if (line.Length == 0 || line.Trim().Length == 0)
            {
                continue;
            }

            var values = SplitLine(line, _delimiter, _quote);
            return new CsvRow(values, _lineNumber, _sourceName);
        }
    }

    private async ValueTask<string?> ReadLineAsync(CancellationToken ct)
    {
        var builder = new StringBuilder(256);

        while (true)
        {
            if (_bufferPosition >= _bufferLength)
            {
                if (_endOfStream)
                {
                    return builder.Length == 0 ? null : builder.ToString();
                }

                _bufferLength = await _reader.ReadAsync(_buffer.AsMemory(), ct);
                _bufferPosition = 0;
                if (_bufferLength == 0)
                {
                    _endOfStream = true;
                    return builder.Length == 0 ? null : builder.ToString();
                }
            }

            var span = _buffer.AsSpan(_bufferPosition, _bufferLength - _bufferPosition);
            var newline = span.IndexOf('\n');
            if (newline < 0)
            {
                builder.Append(span);
                _bufferPosition = _bufferLength;
                continue;
            }

            var slice = span[..newline];
            _bufferPosition += newline + 1;
            if (slice.Length > 0 && slice[^1] == '\r')
            {
                slice = slice[..^1];
            }

            builder.Append(slice);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Splits a delimited row honouring the configured quote character. Source files do not
    /// escape quotes by doubling them, so a quote always toggles the quoted state.
    /// </summary>
    private static List<string> SplitLine(string line, char delimiter, char quote)
    {
        var values = new List<string>(32);
        var current = new StringBuilder(64);
        var quoted = false;

        foreach (var c in line)
        {
            if (quote != '\0' && c == quote)
            {
                quoted = !quoted;
                continue;
            }

            if (c == delimiter && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values;
    }

    private static char DetectDelimiter(string header)
    {
        if (header.Contains(';'))
        {
            return ';';
        }

        if (header.Contains(','))
        {
            return ',';
        }

        if (header.Contains('\t'))
        {
            return '\t';
        }

        throw new InvalidDataException(
            $"Could not detect the delimiter of '{header[..Math.Min(80, header.Length)]}'.");
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// One delimited row addressed by column name.
/// </summary>
public readonly struct CsvRow
{
    private readonly IReadOnlyList<string> _values;

    internal CsvRow(IReadOnlyList<string> values, long lineNumber, string sourceName)
    {
        _values = values;
        LineNumber = lineNumber;
        SourceName = sourceName;
    }

    public long LineNumber { get; }
    public string SourceName { get; }

    public string this[int index] =>
        index >= 0 && index < _values.Count ? _values[index].Trim() : string.Empty;

    public string? Get(IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out var index))
        {
            throw new InvalidDataException($"Column '{name}' is not present in '{SourceName}'.");
        }

        return NullIfEmpty(this[index]);
    }

    public string? GetOptional(IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out var index))
        {
            return null;
        }

        return NullIfEmpty(this[index]);
    }

    public int Count => _values.Count;

    public string Sample(int max = 500)
    {
        var joined = string.Join(';', _values);
        return joined.Length <= max ? joined : joined[..max];
    }

    private static string? NullIfEmpty(string value)
    {
        if (value.Length == 0)
        {
            return null;
        }

        // TSE uses these literals as explicit "not available" markers.
        return value switch
        {
            "#NE" or "#NULO" or "#NULO#" or "#NE#" or "-1" or "-3" or "-4" => null,
            _ => value
        };
    }
}

/// <summary>
/// Conversion helpers tolerant of the source conventions. Invalid values return null instead
/// of throwing so that a malformed row can be rejected individually.
/// </summary>
public static class TseValueParser
{
    public static int? ToInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public static DateOnly? ToDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            ? date
            : null;
    }

    public static DateTime? ToDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                value,
                ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
