using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Electoral.Models;
using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.Candidates;
using Bravo.Infrastructure.Tse.Parsing;
using Bravo.Infrastructure.Tse.Readers;

namespace Bravo.Infrastructure.Tse.Reference;

public sealed class TseReferenceReader : ITseReferenceReader
{
    public TseReferenceSnapshot Read(Stream stream, string layoutVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var layout = CandidateLayouts.Get(layoutVersion);

        // A single candidate file can contain several elections and both rounds, so every
        // distinct (election code, round) pair is retained rather than just the first row.
        var elections = new Dictionary<(int Code, short Round), TseElectionReference>();
        var offices = new Dictionary<int, TseOfficeReference>();
        var parties = new Dictionary<(int, string), TsePartyReference>();
        var federations = new Dictionary<int, TseFederationReference>();

        foreach (var record in new TseRecordReader(layout).Read(stream))
        {
            var values = record.Values;

            var election = new TseElectionReference(
                TseValues.ParseInt(values[Canonical.ElectionCode]),
                TseValues.ParseInt(values[Canonical.Year]),
                TseValues.ParseInt(values[Canonical.ElectionTypeCode]),
                values[Canonical.ElectionTypeName],
                values[Canonical.ElectionDescription],
                TseValues.ParseNullableDate(values[Canonical.ElectionDate]),
                (short)TseValues.ParseInt(values[Canonical.Round]));

            elections[(election.Code, election.Round)] = election;

            var office = new TseOfficeReference(
                TseValues.ParseInt(values[Canonical.OfficeCode]),
                values[Canonical.OfficeName]);

            offices[office.Code] = office;

            var party = new TsePartyReference(
                TseValues.ParseInt(values[Canonical.PartyNumber]),
                values[Canonical.PartyAcronym],
                values[Canonical.PartyName]);

            parties[(party.Number, party.Acronym)] = party;

            if (values.TryGetValue(Canonical.FederationNumber, out var federationNumber)
                && !TseValues.IsSentinel(federationNumber))
            {
                var number = TseValues.ParseInt(federationNumber);

                if (number > 0 && !federations.ContainsKey(number))
                {
                    federations[number] = new TseFederationReference(
                        number,
                        values[Canonical.FederationName],
                        values[Canonical.FederationAcronym],
                        TseValues.Optional(values.GetValueOrDefault(Canonical.FederationComposition)));
                }
            }
        }

        return new TseReferenceSnapshot(
            elections.Values.OrderBy(e => e.Code).ThenBy(e => e.Round).ToArray(),
            offices.Values.OrderBy(office => office.Code).ToArray(),
            parties.Values.OrderBy(party => party.Number).ThenBy(party => party.Acronym).ToArray(),
            federations.Values.OrderBy(federation => federation.Number).ToArray());
    }
}
