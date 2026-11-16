using System.Text;

namespace Bravo.Infrastructure.Tse.Csv;

/// <summary>
/// Parses RFC 4180-style delimited records from a text stream.
/// </summary>
/// <remarks>
/// Handles quoted fields, doubled quotes as an escape, embedded delimiters and embedded
/// line breaks. Records are yielded lazily so arbitrarily large files are never
/// materialised in memory.
/// </remarks>
public sealed class TseCsvReader(TextReader reader, char delimiter, char quote) : IDisposable
{
    private readonly TextReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public IEnumerable<IReadOnlyList<string>> ReadRecords()
    {
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var hasContent = false;

        while (true)
        {
            var value = _reader.Read();

            if (value < 0)
            {
                if (hasContent || field.Length > 0 || record.Count > 0)
                {
                    record.Add(field.ToString());
                    yield return record;
                }

                yield break;
            }

            var character = (char)value;

            if (inQuotes)
            {
                if (character == quote)
                {
                    if (_reader.Peek() == quote)
                    {
                        _reader.Read();
                        field.Append(quote);
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                field.Append(character);
                continue;
            }

            if (character == quote)
            {
                inQuotes = true;
                hasContent = true;
                continue;
            }

            if (character == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
                hasContent = true;
                continue;
            }

            if (character is '\r' or '\n')
            {
                if (character == '\r' && _reader.Peek() == '\n')
                    _reader.Read();

                record.Add(field.ToString());

                if (record.Count > 1 || record[0].Length > 0 || hasContent)
                    yield return record;

                record = [];
                field.Clear();
                hasContent = false;
                continue;
            }

            field.Append(character);
            hasContent = true;
        }
    }

    public void Dispose() => _reader.Dispose();
}
