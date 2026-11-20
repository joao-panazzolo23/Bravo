using Bravo.Infrastructure.Tse.Csv;
using Bravo.Infrastructure.Tse.Layouts;

namespace Bravo.Infrastructure.Tse.Readers;

/// <summary>
/// Streams canonical records from a layout-described delimited file.
/// </summary>
/// <remarks>
/// The header row is consumed once to build the column map. Every subsequent row is
/// projected onto canonical field names, so callers never depend on source-specific
/// column ordering or naming.
/// </remarks>
internal sealed class TseRecordReader
{
    private readonly TseLayout _layout;

    public TseRecordReader(TseLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
    }

    public TseLayout Layout => _layout;

    public IEnumerable<TseRecord> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var rows = TseCsvFile
            .ReadRows(stream, _layout.Encoding, _layout.Delimiter, _layout.Quote)
            .GetEnumerator();

        if (!rows.MoveNext())
            yield break;

        var mapper = TseCsvRecordMapper.Create(_layout, rows.Current);

        mapper.ThrowIfLayoutIncomplete();

        while (rows.MoveNext())
            yield return mapper.Map(rows.Current);
    }
}
