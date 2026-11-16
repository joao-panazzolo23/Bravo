using System.Text;
using Bravo.Infrastructure.Tse.Csv;
using Bravo.Infrastructure.Tse.Layouts;
using Bravo.Infrastructure.Tse.Layouts.Candidates;
using Bravo.Infrastructure.Tse.Readers;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

public sealed class TseRecordReaderTests
{
    private static readonly string[] Header2022 =
    [
        "DT_GERACAO", "HH_GERACAO", "ANO_ELEICAO", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO",
        "NR_TURNO", "CD_ELEICAO", "DS_ELEICAO", "DT_ELEICAO", "SG_UF", "CD_CARGO", "DS_CARGO",
        "SQ_CANDIDATO", "NR_CANDIDATO", "NM_CANDIDATO", "NR_PARTIDO", "SG_PARTIDO", "NM_PARTIDO",
        "NR_FEDERACAO", "DS_EMAIL"
    ];

    private static string BuildFile(string[] header, params string[][] data)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', header.Select(value => $"\"{value}\"")));

        foreach (var row in data)
            builder.AppendLine(string.Join(';', row.Select(value => $"\"{value}\"")));

        return builder.ToString();
    }

    private static List<TseRecord> Read(string content, TseLayout layout)
    {
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(content));
        return new TseRecordReader(layout).Read(stream).ToList();
    }

    [Fact]
    public void MapsSourceColumnsToCanonicalNames()
    {
        var content = BuildFile(
            Header2022,
            ["14/11/2026", "03:15:10", "2022", "2", "ELEIÇÃO ORDINÁRIA", "1", "546",
             "Eleições Gerais", "02/10/2022", "AC", "7", "DEPUTADO ESTADUAL", "10001595357",
             "50510", "GILBERTO ALVES", "50", "PSOL", "PARTIDO SOCIALISMO", "3", "a@b.c"]);

        var record = Read(content, CandidateLayouts.V2022()).Single();

        record.Values[Canonical.Year].Should().Be("2022");
        record.Values[Canonical.Round].Should().Be("1");
        record.Values[Canonical.StateAcronym].Should().Be("AC");
        record.Values[Canonical.CandidateSequential].Should().Be("10001595357");
        record.Values[Canonical.PartyAcronym].Should().Be("PSOL");
        record.Values[Canonical.ElectionTypeName].Should().Be("ELEIÇÃO ORDINÁRIA");
    }

    [Fact]
    public void IgnoresSourceColumnsAbsentFromTheLayout()
    {
        var content = BuildFile(
            Header2022,
            ["d", "h", "2022", "2", "T", "1", "546", "D", "02/10/2022", "AC", "7", "O",
             "1", "2", "N", "50", "PSOL", "P", "3", "a@b.c"]);

        var record = Read(content, CandidateLayouts.V2022()).Single();

        record.Values.Should().NotContainKey("DT_GERACAO");
        record.Values.Should().NotContainKey("HH_GERACAO");
    }

    [Fact]
    public void MapsHistoricalEmailColumnFrom2010Layout()
    {
        var header2010 = new[]
        {
            "ANO_ELEICAO", "NM_EMAIL", "CD_ELEICAO", "NR_TURNO", "NM_CANDIDATO",
            "NR_PARTIDO", "SG_PARTIDO", "NM_PARTIDO", "SQ_CANDIDATO", "NR_CANDIDATO",
            "CD_CARGO", "DS_CARGO", "SG_UF", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO",
            "DS_ELEICAO", "DT_ELEICAO"
        };

        var content = BuildFile(
            header2010,
            ["2010", "legacy@tse.gov.br", "1", "1", "FULANO", "13", "PT", "PARTIDO DOS TRABALHADORES",
             "9000000001", "1313", "1", "PRESIDENTE", "BR", "1", "ELEIÇÃO ORDINÁRIA",
             "Eleições 2010", "03/10/2010"]);

        var record = Read(content, CandidateLayouts.V2010()).Single();

        record.Values[Canonical.Email].Should().Be("legacy@tse.gov.br");
        record.Values[Canonical.Year].Should().Be("2010");
    }

    [Fact]
    public void SameCanonicalFieldsResolveFromBothLayoutVersions()
    {
        var header2010 = new[]
        {
            "ANO_ELEICAO", "NM_EMAIL", "CD_ELEICAO", "NR_TURNO", "NM_CANDIDATO",
            "NR_PARTIDO", "SG_PARTIDO", "NM_PARTIDO", "SQ_CANDIDATO", "NR_CANDIDATO",
            "CD_CARGO", "DS_CARGO", "SG_UF", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO",
            "DS_ELEICAO", "DT_ELEICAO"
        };

        var modern = BuildFile(
            Header2022,
            ["d", "h", "2022", "2", "T", "1", "546", "D", "02/10/2022", "SP", "6", "DEPUTADO FEDERAL",
             "1", "1234", "NOME", "13", "PT", "PARTIDO", "1", "new@tse.gov.br"]);

        var legacy = BuildFile(
            header2010,
            ["2010", "old@tse.gov.br", "1", "1", "NOME", "13", "PT", "PARTIDO",
             "1", "1234", "6", "DEPUTADO FEDERAL", "SP", "1", "T", "D", "03/10/2010"]);

        var modernRecord = Read(modern, CandidateLayouts.V2022()).Single();
        var legacyRecord = Read(legacy, CandidateLayouts.V2010()).Single();

        modernRecord.Values.Keys.Intersect(legacyRecord.Values.Keys)
            .Should().Contain([Canonical.Year, Canonical.StateAcronym, Canonical.PartyAcronym,
                Canonical.CandidateSequential, Canonical.Email]);
        modernRecord.Values[Canonical.Email].Should().Be("new@tse.gov.br");
        legacyRecord.Values[Canonical.Email].Should().Be("old@tse.gov.br");
    }

    [Fact]
    public void FailsExplicitlyWhenARequiredColumnIsMissing()
    {
        var header = new[] { "ANO_ELEICAO", "NR_TURNO" };
        var content = BuildFile(header, ["2022", "1"]);

        var act = () => Read(content, CandidateLayouts.V2022());

        act.Should().Throw<TseLayoutMismatchException>()
            .WithMessage("*CD_CARGO*");
    }

    [Fact]
    public void ToleratesColumnsTheLayoutDoesNotDeclare()
    {
        var header = Header2022.Append("UNKNOWN_COLUMN").ToArray();
        var content = BuildFile(
            header,
            ["d", "h", "2022", "2", "T", "1", "546", "D", "02/10/2022", "AC", "7", "O",
             "1", "2", "N", "50", "PSOL", "P", "3", "a@b.c", "ignored"]);

        var record = Read(content, CandidateLayouts.V2022()).Single();

        record.Values[Canonical.Year].Should().Be("2022");
        record.Values.Should().NotContainValue("ignored");
    }

    [Fact]
    public void ReturnsEmptyWhenFileHasOnlyAHeader()
    {
        var content = BuildFile(Header2022);

        Read(content, CandidateLayouts.V2022()).Should().BeEmpty();
    }

    [Fact]
    public void ReturnsEmptyForEmptyStream()
    {
        Read(string.Empty, CandidateLayouts.V2022()).Should().BeEmpty();
    }

    [Fact]
    public void ResolvesLayoutByVersionString()
    {
        CandidateLayouts.Get(CandidateLayouts.Version2022).Version
            .Should().Be(CandidateLayouts.Version2022);

        CandidateLayouts.Get(CandidateLayouts.Version2010).Version
            .Should().Be(CandidateLayouts.Version2010);
    }

    [Fact]
    public void RejectsUnknownLayoutVersion()
    {
        var act = () => CandidateLayouts.Get("cand-1999-v1");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
