using Bravo.Domain.Electoral.Models;

namespace Bravo.Domain.Electoral.Services;

/// <summary>
/// Streams section-result rows from a TSE results file.
/// </summary>
/// <remarks>
/// Results files reach several gigabytes decompressed and contain millions of rows, so the
/// reader must never materialise the file. Each enumerator owns its stream position: callers
/// may enumerate the same file more than once (the import does a geographic pass and a results
/// pass), and each pass starts from the beginning and holds one row at a time.
/// </remarks>
public interface ITseSectionResultReader
{
    IAsyncEnumerable<TseSectionResultRow> ReadAsync(
        Stream stream,
        string layoutVersion,
        CancellationToken cancellationToken = default);
}
