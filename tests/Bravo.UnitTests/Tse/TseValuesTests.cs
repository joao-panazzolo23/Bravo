using Bravo.Infrastructure.Tse.Parsing;
using FluentAssertions;

namespace Bravo.UnitTests.Tse;

public sealed class TseValuesTests
{
    [Theory]
    [InlineData("04/10/1998")]
    [InlineData("28/07/1971")]
    public void ParsesValidDates(string value)
    {
        TseValues.ParseNullableDate(value).Should().Be(DateOnly.ParseExact(
            value, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#NULO")]
    [InlineData("#NE")]
    [InlineData("-1")]
    public void TreatsSentinelsAsMissing(string? value)
    {
        TseValues.ParseNullableDate(value).Should().BeNull();
    }

    [Theory]
    [InlineData("00/00/1900")]
    [InlineData("00/00/0000")]
    public void TreatsPlaceholderDateAsMissing(string value)
    {
        TseValues.ParseNullableDate(value).Should().BeNull();
    }

    [Fact]
    public void CorruptDateResolvesToNullInsteadOfThrowing()
    {
        // Verified defect in the real 1998/RJ file.
        TseValues.ParseNullableDate("15/64/6514").Should().BeNull();
    }

    [Theory]
    [InlineData("-19,0348876", -19.0348876)]
    [InlineData("-43,4224884", -43.4224884)]
    [InlineData("0", 0)]
    public void ParsesCommaDecimalCoordinates(string value, decimal expected)
    {
        TseValues.ParseNullableCoordinate(value).Should().Be(expected);
    }

    [Fact]
    public void CandidateSequentialPreservesSlashFormattedValues()
    {
        // Historical SQ_CANDIDATO values are not always numeric, so they are stored as text.
        TseValues.Optional("15/64/6514").Should().Be("15/64/6514");
    }
}
