using System.Text;

namespace Bravo.Infrastructure.Tse.Csv;

public static class TseCsvFile
{
    public static IEnumerable<IReadOnlyList<string>> ReadRows(
        Stream stream,
        Encoding encoding,
        char delimiter,
        char quote)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoding);

        using var reader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 64 * 1024,
            leaveOpen: true);

        using var csv = new TseCsvReader(reader, delimiter, quote);

        foreach (var record in csv.ReadRecords())
            yield return record;
    }
}
