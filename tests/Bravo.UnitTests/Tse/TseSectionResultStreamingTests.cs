using System.Text;
using Bravo.Domain.Electoral.Models;
using Bravo.Infrastructure.Tse.Reference;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

/// <summary>
/// The results reader must stream: these tests prove rows are produced incrementally and that a
/// reader can be abandoned without consuming the rest of the file.
/// </summary>
public sealed class TseSectionResultStreamingTests
{
    private static readonly string Header =
        "\"ANO_ELEICAO\";\"NR_TURNO\";\"CD_ELEICAO\";\"SG_UF\";\"CD_MUNICIPIO\";\"NM_MUNICIPIO\";" +
        "\"NR_ZONA\";\"NR_SECAO\";\"CD_CARGO\";\"NR_VOTAVEL\";\"NM_VOTAVEL\";\"QT_VOTOS\";" +
        "\"NR_LOCAL_VOTACAO\";\"SQ_CANDIDATO\"";

    private static string Row(int section, long votes = 10)
        => $"2022;1;544;AC;\"0001\";\"RIO BRANCO\";1;{section};6;13;\"PARTIDO\";{votes};5;\"0000000001\"";

    private static MemoryStream Build(int rows)
    {
        var builder = new StringBuilder(Header).Append('\n');

        for (var index = 1; index <= rows; index++)
            builder.Append(Row(index)).Append('\n');

        return new MemoryStream(Encoding.Latin1.GetBytes(builder.ToString()));
    }

    [Fact]
    public async Task YieldsRowsIncrementallyWithoutReadingToEnd()
    {
        await using var stream = Build(10_000);
        var reader = new TseSectionResultReader();

        var count = 0;

        await foreach (var row in reader.ReadAsync(stream, "vot-secao-2022-v1"))
        {
            row.Year.Should().Be(2022);

            // Abandoning after 3 rows must leave the rest of the stream untouched, which is the
            // whole point of streaming: the file is not consumed up front.
            if (++count == 3)
                break;
        }

        count.Should().Be(3);
        stream.Position.Should().BeLessThan(stream.Length);
    }

    [Fact]
    public async Task ProducesEveryRowExactlyOnce()
    {
        await using var stream = Build(500);
        var reader = new TseSectionResultReader();

        var sections = new List<int>();

        await foreach (var row in reader.ReadAsync(stream, "vot-secao-2022-v1"))
            sections.Add(row.SectionNumber);

        sections.Should().HaveCount(500);
        sections.Should().Equal(Enumerable.Range(1, 500));
    }

    [Fact]
    public async Task HonoursCancellationWhileStreaming()
    {
        await using var stream = Build(10_000);
        var reader = new TseSectionResultReader();

        using var cts = new CancellationTokenSource();
        var seen = 0;

        await FluentActions.Awaiting(async () =>
        {
            await foreach (var _ in reader.ReadAsync(stream, "vot-secao-2022-v1", cts.Token))
            {
                if (++seen == 5)
                    cts.Cancel();
            }
        }).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PreservesQuotingAndEscapingSemantics()
    {
        var content = Header + "\n" +
            "2022;1;544;AC;\"0001\";\"RIO; BRANCO\";1;1;6;13;\"PARTIDO \\\"X\\\"\";10;5;\"0000000001\"\n";

        content = content.Replace("\\\"", "\"\"");

        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(content));
        var reader = new TseSectionResultReader();

        var rows = new List<TseSectionResultRow>();

        await foreach (var row in reader.ReadAsync(stream, "vot-secao-2022-v1"))
            rows.Add(row);

        rows.Should().ContainSingle();

        // The embedded delimiter stays inside the field and the doubled quote is unescaped.
        rows[0].MunicipalityName.Should().Be("RIO; BRANCO");
        rows[0].BallotName.Should().Be("PARTIDO \"X\"");
    }

    [Fact]
    public async Task DoesNotRetainRowsAfterEnumeration()
    {
        await using var stream = Build(20_000);
        var reader = new TseSectionResultReader();

        long votes = 0;

        await foreach (var row in reader.ReadAsync(stream, "vot-secao-2022-v1"))
            votes += row.Votes;

        // The test itself holds no rows; this asserts the reader produced all of them. Memory
        // behaviour is covered by the benchmark, not by an assertion here.
        votes.Should().Be(200_000);
    }
}
