using System.Text;
using Bravo.Infrastructure.Tse.Csv;
using Bravo.Infrastructure.Tse.Layouts;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

public sealed class TseCsvReaderTests
{
    private static IReadOnlyList<IReadOnlyList<string>> Read(string content, char delimiter = ';')
    {
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(content));
        return TseCsvFile.ReadRows(stream, Encoding.Latin1, delimiter, '"').ToList();
    }

    [Fact]
    public void ReadsDelimiterSeparatedFields()
    {
        var rows = Read("A;B;C\n1;2;3\n");

        rows.Should().HaveCount(2);
        rows[0].Should().Equal("A", "B", "C");
        rows[1].Should().Equal("1", "2", "3");
    }

    [Fact]
    public void ReadsQuotedFieldsContainingTheDelimiter()
    {
        var rows = Read("\"a;b\";c\n");

        rows[0].Should().Equal("a;b", "c");
    }

    [Fact]
    public void UnescapesDoubledQuotes()
    {
        var rows = Read("\"say \"\"hello\"\"\";x\n");

        rows[0][0].Should().Be("say \"hello\"");
        rows[0][1].Should().Be("x");
    }

    [Fact]
    public void ReadsQuotedFieldsContainingLineBreaks()
    {
        var rows = Read("\"line1\nline2\";b\n");

        rows.Should().HaveCount(1);
        rows[0][0].Should().Be("line1\nline2");
        rows[0][1].Should().Be("b");
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void HandlesAllLineEndingStyles(string newLine)
    {
        var content = $"A;B{newLine}1;2{newLine}";

        var rows = Read(content);

        rows.Should().HaveCount(2);
        rows[1].Should().Equal("1", "2");
    }

    [Fact]
    public void PreservesLatin1Characters()
    {
        var rows = Read("\"ELEIÇÃO ORDINÁRIA\";\"SÃO PAULO\"\n");

        rows[0][0].Should().Be("ELEIÇÃO ORDINÁRIA");
        rows[0][1].Should().Be("SÃO PAULO");
    }

    [Fact]
    public void ReadsUtf8ContentWhenUtf8EncodingIsSpecified()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("A;B\nEleição;Ação\n"));

        var rows = TseCsvFile.ReadRows(stream, Encoding.UTF8, ';', '"').ToList();

        rows[1][0].Should().Be("Eleição");
    }

    [Fact]
    public void DoesNotEmitTrailingEmptyRecord()
    {
        var rows = Read("A;B\n1;2\r\n");

        rows.Should().HaveCount(2);
    }

    [Fact]
    public void HandlesEmptyFields()
    {
        var rows = Read("A;;C\n1;;3\n");

        rows[0].Should().Equal("A", "", "C");
        rows[1].Should().Equal("1", "", "3");
    }

    [Fact]
    public void SupportsAlternativeDelimiter()
    {
        var rows = Read("A,B\n1,2\n", delimiter: ',');

        rows[1].Should().Equal("1", "2");
    }

    [Fact]
    public void StreamsWithoutMaterialisingWholeFile()
    {
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes("A;B\n1;2\n3;4\n"));

        using var enumerator = TseCsvFile.ReadRows(stream, Encoding.Latin1, ';', '"').GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Equal("A", "B");
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Equal("1", "2");
    }
}
