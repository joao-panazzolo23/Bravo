using System.Runtime.CompilerServices;
using Bravo.Domain.Electoral.Enums;
using Bravo.Domain.Electoral.Models;
using Bravo.Domain.Electoral.Services;
using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.SectionResults;
using Bravo.Infrastructure.Tse.Parsing;
using Bravo.Infrastructure.Tse.Readers;

namespace Bravo.Infrastructure.Tse.Reference;

/// <summary>
/// Streams section-result rows from a TSE results file, one record at a time.
/// </summary>
/// <remarks>
/// The underlying CSV reader is already lazy, so this type only maps a record to
/// <see cref="TseSectionResultRow"/> as it is pulled. Nothing accumulates: peak memory is the
/// size of one CSV record plus the current row, regardless of file size.
///
/// Fields are read through the mapper's canonical index rather than the per-row dictionary
/// view, because a large results file produces tens of millions of rows and the difference is
/// the difference between a few hundred megabytes of garbage and tens of gigabytes.
/// </remarks>
public sealed class TseSectionResultReader : ITseSectionResultReader
{

    public async IAsyncEnumerable<TseSectionResultRow> ReadAsync(
        Stream stream,
        string layoutVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var layout = SectionResultLayouts.Get(layoutVersion);

        // Parsing is CPU-bound over a local file, so it stays synchronous; the async shape
        // exists so callers can compose the reader with asynchronous persistence. Yielding
        // once here satisfies the iterator contract without adding per-row overhead.
        await Task.CompletedTask;

        foreach (var record in new TseRecordReader(layout).Read(stream))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new TseSectionResultRow(
                Year: TseValues.ParseInt(record[Canonical.Year]),
                Round: (short)TseValues.ParseInt(record[Canonical.Round]),
                ElectionCode: TseValues.ParseInt(record[Canonical.ElectionCode]),
                StateAcronym: record[Canonical.StateAcronym],
                TseMunicipalityCode: record[Canonical.MunicipalityCode].Trim(),
                MunicipalityName: record[Canonical.MunicipalityName],
                ZoneNumber: TseValues.ParseInt(record[Canonical.ZoneNumber]),
                SectionNumber: TseValues.ParseInt(record[Canonical.SectionNumber]),
                OfficeCode: TseValues.ParseInt(record[Canonical.OfficeCode]),
                BallotNumber: TseValues.ParseInt(record[Canonical.BallotNumber]),
                BallotName: record[Canonical.BallotName],
                Votes: TseValues.ParseLong(record[Canonical.Votes]),
                PollingPlaceNumber: TseValues.ParseInt(record[Canonical.PollingPlaceNumber]),
                CandidateSequential: TseValues.Optional(
                    record.GetValue(Canonical.CandidateSequential)));
        }
    }
}
