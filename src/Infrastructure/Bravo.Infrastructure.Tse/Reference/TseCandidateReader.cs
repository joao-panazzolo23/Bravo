using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Models;
using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.Candidates;
using Bravo.Infrastructure.Tse.Parsing;
using Bravo.Infrastructure.Tse.Readers;

namespace Bravo.Infrastructure.Tse.Reference;

public sealed class TseCandidateReader : ITseCandidateReader
{
    private const string NoCoalitionMarker = "PARTIDO ISOLADO";

    public IReadOnlyList<TseCandidateRow> Read(Stream stream, string layoutVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var layout = CandidateLayouts.Get(layoutVersion);
        var rows = new List<TseCandidateRow>();

        foreach (var record in new TseRecordReader(layout).Read(stream))
        {
            var values = record.Values;

            var aggregationType = values.GetValueOrDefault(Canonical.AggregationType)?.Trim() ?? string.Empty;
            var hasCoalition = !string.Equals(aggregationType, NoCoalitionMarker, StringComparison.OrdinalIgnoreCase);

            rows.Add(new TseCandidateRow(
                ElectionCode: TseValues.ParseInt(values[Canonical.ElectionCode]),
                Round: (short)TseValues.ParseInt(values[Canonical.Round]),
                Sequential: values[Canonical.CandidateSequential].Trim(),
                BallotNumber: TseValues.ParseInt(values[Canonical.BallotNumber]),
                OfficeCode: TseValues.ParseInt(values[Canonical.OfficeCode]),
                OfficeName: values[Canonical.OfficeName],
                StateAcronym: values[Canonical.StateAcronym],
                PartyNumber: TseValues.ParseInt(values[Canonical.PartyNumber]),
                PartyAcronym: values[Canonical.PartyAcronym],
                PartyName: values[Canonical.PartyName],
                FederationNumber: TseValues.ParseNullableInt(values.GetValueOrDefault(Canonical.FederationNumber)),
                FederationName: TseValues.Optional(values.GetValueOrDefault(Canonical.FederationName)),
                FederationAcronym: TseValues.Optional(values.GetValueOrDefault(Canonical.FederationAcronym)),
                CoalitionNumber: hasCoalition
                    ? TseValues.ParseNullableLong(values.GetValueOrDefault(Canonical.CoalitionNumber))
                    : null,
                CoalitionName: hasCoalition
                    ? TseValues.Optional(values.GetValueOrDefault(Canonical.CoalitionName))
                    : null,
                CoalitionComposition: hasCoalition
                    ? TseValues.Optional(values.GetValueOrDefault(Canonical.CoalitionComposition))
                    : null,
                Name: values[Canonical.CandidateName],
                UrnName: values[Canonical.UrnName],
                SocialName: TseValues.Optional(values.GetValueOrDefault(Canonical.SocialName)),
                Cpf: TseValues.Optional(values.GetValueOrDefault(Canonical.Cpf)),
                RegistrationStatus: TseValues.Optional(values.GetValueOrDefault(Canonical.RegistrationStatus)),
                Gender: TseValues.Optional(values.GetValueOrDefault(Canonical.Gender)),
                Education: TseValues.Optional(values.GetValueOrDefault(Canonical.Education)),
                Race: TseValues.Optional(values.GetValueOrDefault(Canonical.Race)),
                Occupation: TseValues.Optional(values.GetValueOrDefault(Canonical.Occupation)),
                BirthDate: TseValues.ParseNullableDate(values.GetValueOrDefault(Canonical.BirthDate)),
                HasCoalition: hasCoalition));
        }

        return rows;
    }
}
