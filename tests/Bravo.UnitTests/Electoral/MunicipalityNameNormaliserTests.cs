using Bravo.Application.Electoral.Services;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

public sealed class MunicipalityNameNormaliserTests
{
    [Theory]
    [InlineData("São Paulo", "SAO PAULO")]
    [InlineData("SÃO PAULO", "SAO PAULO")]
    [InlineData("  São   Paulo  ", "SAO PAULO")]
    [InlineData("Conceição do Mato Dentro", "CONCEICAO DO MATO DENTRO")]
    [InlineData("Alta Floresta D'Oeste", "ALTA FLORESTA D OESTE")]
    [InlineData("Brasília", "BRASILIA")]
    public void NormalisesAccentsCaseAndPunctuation(string input, string expected)
    {
        MunicipalityNameNormaliser.Normalise(input).Should().Be(expected);
    }

    [Fact]
    public void KeepsGenuinelyDifferentNamesDistinct()
    {
        var a = MunicipalityNameNormaliser.Normalise("Bom Jesus");
        var b = MunicipalityNameNormaliser.Normalise("Bom Jesus do Norte");

        a.Should().NotBe(b);
    }
}
