using System.IO.Compression;
using System.Text;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Storage;
using FluentAssertions;

namespace Bravo.UnitTests.Acquisition;

/// <summary>
/// Tests the TSE archive parsing against real-shaped fixtures: semicolon separated, Latin-1,
/// quoted values, one CSV per state plus a national aggregate, and years with and without
/// optional columns.
/// </summary>
public class TseArchiveParsingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bravo-tse-tests", Guid.NewGuid().ToString("N"));

    public TseArchiveParsingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private const string Header =
        "\"DT_GERACAO\";\"HH_GERACAO\";\"ANO_ELEICAO\";\"CD_TIPO_ELEICAO\";\"NM_TIPO_ELEICAO\";\"NR_TURNO\";\"CD_ELEICAO\";" +
        "\"DS_ELEICAO\";\"DT_ELEICAO\";\"TP_ABRANGENCIA\";\"SG_UF\";\"SG_UE\";\"NM_UE\";\"CD_MUNICIPIO\";\"NM_MUNICIPIO\";" +
        "\"NR_ZONA\";\"NR_SECAO\";\"CD_CARGO\";\"DS_CARGO\";\"NR_VOTAVEL\";\"NM_VOTAVEL\";\"QT_VOTOS\";\"NR_LOCAL_VOTACAO\";" +
        "\"SQ_CANDIDATO\";\"NM_LOCAL_VOTACAO\";\"DS_LOCAL_VOTACAO_ENDERECO\"";

    private static string Row(params string[] values) =>
        string.Join(';', values.Select(v => $"\"{v}\""));

    private string CreateArchive(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var stream = entry.Open();
            var bytes = Encoding.Latin1.GetBytes(content);
            stream.Write(bytes);
        }

        return path;
    }

    [Fact]
    public void SelectEntries_ShouldPreferPerStateFilesOverNationalAggregates()
    {
        var path = CreateArchive(
            ("votacao_secao_2022_BR.csv", Header),
            ("votacao_secao_2022_BRASIL.csv", Header),
            ("votacao_secao_2022_AC.csv", Header),
            ("votacao_secao_2022_AL.csv", Header),
            ("leiame.pdf", "x"));

        using var archive = ZipFile.OpenRead(path);
        var entries = TseArchiveStreams.SelectEntries(archive);

        entries.Select(e => e.Name).Should().BeEquivalentTo(
            ["votacao_secao_2022_AC.csv", "votacao_secao_2022_AL.csv"]);
    }

    [Fact]
    public void SelectEntries_ShouldUseSingleNationalFileWhenNoStateFilesExist()
    {
        var path = CreateArchive(("votacao_secao_2022_BR.csv", Header));
        using var archive = ZipFile.OpenRead(path);

        var entries = TseArchiveStreams.SelectEntries(archive);

        entries.Should().ContainSingle().Which.Name.Should().Be("votacao_secao_2022_BR.csv");
    }

    [Fact]
    public async Task ReadPollingSectionResults_ShouldNormalizeMunicipalityCodeAndClassifyVotes()
    {
        var content = string.Join('\n',
            Header,
            Row("01/11/2022", "16:05:25", "2022", "2", "Eleição Ordinária", "1", "546", "Eleições", "02/10/2022", "E",
                "AC", "AC", "ACRE", "1392", "RIO BRANCO", "9", "24", "3", "Governador", "13", "CANDIDATO A", "10",
                "1384", "10001616169", "ESCOLA X", "RUA Y"),
            Row("01/11/2022", "16:05:25", "2022", "2", "Eleição Ordinária", "1", "546", "Eleições", "02/10/2022", "E",
                "AC", "AC", "ACRE", "1392", "RIO BRANCO", "9", "24", "3", "Governador", "95", "VOTO BRANCO", "2",
                "1384", "-1", "ESCOLA X", "RUA Y"),
            Row("01/11/2022", "16:05:25", "2022", "2", "Eleição Ordinária", "1", "546", "Eleições", "02/10/2022", "E",
                "AC", "AC", "ACRE", "1392", "RIO BRANCO", "9", "24", "3", "Governador", "96", "VOTO NULO", "3",
                "1384", "-1", "ESCOLA X", "RUA Y"));

        var path = CreateArchive(("votacao_secao_2022_AC.csv", content));
        var dataset = new TseDatasetInfo(TseDatasetKind.PollingSectionResults, "x", "x", new Uri("https://cdn.tse.jus.br/x.zip"), "CSV", 2022, null);

        var records = new List<TsePollingSectionResultRecord>();
        await foreach (var parsed in TseArchiveStreams.ReadPollingSectionResults(path, dataset, "AC"))
        {
            parsed.Record.Should().NotBeNull();
            records.Add(parsed.Record!);
        }

        records.Should().HaveCount(3);
        records[0].MunicipalityTseCode.Should().Be("01392", "the official code is zero padded");
        records[0].CandidateSequential.Should().Be("10001616169");
        records[0].IsBlank.Should().BeFalse();
        records[1].IsBlank.Should().BeTrue();
        records[1].CandidateSequential.Should().BeNull();
        records[2].IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task ReadCandidates_ShouldRejectRowsMissingMandatoryColumns()
    {
        var candidateHeader = Header.Replace("\"QT_VOTOS\";", "");
        var content = string.Join('\n',
            candidateHeader,
            Row("14/11/2026", "03:15:10", "2022", "2", "ELEIÇÃO", "1", "546", "Eleições", "02/10/2022", "ESTADUAL",
                "MS", "MS", "MATO GROSSO DO SUL", "7", "DEPUTADO", "120001648882", "90000", "NOME", "URNA", "CANDIDATO A",
                "10", "PARTIDO", "PARTIDO X", "1384", "10001616169", "ESCOLA", "RUA"));

        // Header is incompatible with the candidate columns on purpose: the parser must fail
        // fast rather than shift values.
        var path = CreateArchive(("consulta_cand_2022_MS.csv", content));
        var dataset = new TseDatasetInfo(TseDatasetKind.Candidates, "x", "x", new Uri("https://cdn.tse.jus.br/x.zip"), "CSV", 2022, null);

        var act = async () =>
        {
            await foreach (var _ in TseArchiveStreams.ReadCandidates(path, dataset, "MS"))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public void ArchiveGuard_ShouldRejectEntryThatEscapesTheArchive()
    {
        var path = CreateArchive(("../evil.csv", Header));
        using var archive = ZipFile.OpenRead(path);

        var act = () => ArchiveGuard.GetCsvEntries(archive, 1024);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ArchiveGuard_ShouldRejectArchivesAboveDecompressedLimit()
    {
        var path = CreateArchive(("big.csv", new string('a', 5000)));
        using var archive = ZipFile.OpenRead(path);

        var act = () => ArchiveGuard.GetCsvEntries(archive, 1024);

        act.Should().Throw<InvalidDataException>();
    }
}
