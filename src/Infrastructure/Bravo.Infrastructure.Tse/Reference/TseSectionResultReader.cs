using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Models;
using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.SectionResults;
using Bravo.Infrastructure.Tse.Parsing;
using Bravo.Infrastructure.Tse.Readers;

namespace Bravo.Infrastructure.Tse.Reference;

public sealed class TseSectionResultReader : ITseSectionResultReader
{
    public const int BlankVoteCode = 95;
    public const int NullVoteCode = 96;

    public IReadOnlyList<TseSectionResultRow> Read(Stream stream, string layoutVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var layout = SectionResultLayouts.Get(layoutVersion);
        var rows = new List<TseSectionResultRow>();

        foreach (var record in new TseRecordReader(layout).Read(stream))
        {
            var values = record.Values;

            rows.Add(new TseSectionResultRow(
                Year: TseValues.ParseInt(values[Canonical.Year]),
                Round: (short)TseValues.ParseInt(values[Canonical.Round]),
                ElectionCode: TseValues.ParseInt(values[Canonical.ElectionCode]),
                StateAcronym: values[Canonical.StateAcronym],
                TseMunicipalityCode: values[Canonical.MunicipalityCode].Trim(),
                MunicipalityName: values[Canonical.MunicipalityName],
                ZoneNumber: TseValues.ParseInt(values[Canonical.ZoneNumber]),
                SectionNumber: TseValues.ParseInt(values[Canonical.SectionNumber]),
                OfficeCode: TseValues.ParseInt(values[Canonical.OfficeCode]),
                BallotNumber: TseValues.ParseInt(values[Canonical.BallotNumber]),
                BallotName: values[Canonical.BallotName],
                Votes: TseValues.ParseLong(values[Canonical.Votes]),
                PollingPlaceNumber: TseValues.ParseInt(values[Canonical.PollingPlaceNumber]),
                CandidateSequential: TseValues.Optional(
                    values.GetValueOrDefault(Canonical.CandidateSequential))));
        }

        return rows;
    }
}
