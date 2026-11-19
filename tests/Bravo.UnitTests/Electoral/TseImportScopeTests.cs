using Bravo.Application.Importing;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

public sealed class TseImportScopeTests
{
    [Fact]
    public void BuildsAndParsesReferenceAndCandidateKeys()
    {
        var reference = TseImport.ReferenceKey(2022);
        var candidates = TseImport.CandidatesKey(2022);

        TseImport.TryParseYear(reference, TseImport.ReferenceStage, out var referenceYear)
            .Should().BeTrue();
        TseImport.TryParseYear(candidates, TseImport.CandidatesStage, out var candidateYear)
            .Should().BeTrue();

        referenceYear.Should().Be(2022);
        candidateYear.Should().Be(2022);
    }

    [Fact]
    public void BuildsAndParsesResultKeysWithState()
    {
        var key = TseImport.ResultsKey(2022, "ac");

        TseImport.TryParseResults(key, out var year, out var state).Should().BeTrue();

        year.Should().Be(2022);
        state.Should().Be("AC");
    }

    [Fact]
    public void ReferenceAndCandidateKeysDoNotCollide()
    {
        TseImport.ReferenceKey(2022).Should().NotBe(TseImport.CandidatesKey(2022));
    }

    [Theory]
    [InlineData("")]
    [InlineData("TseResults")]
    [InlineData("TseResults:2022")]
    [InlineData("TseResults:abc:AC")]
    public void RejectsMalformedResultKeys(string key)
    {
        TseImport.TryParseResults(key, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void RejectsWrongStageWhenParsingYear()
    {
        TseImport.TryParseYear(TseImport.ResultsKey(2022, "AC"), TseImport.CandidatesStage, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void RecognisesItsOwnKeys()
    {
        TseImport.IsTseUnitKey(TseImport.ReferenceKey(2022)).Should().BeTrue();
        TseImport.IsTseUnitKey(TseImport.ResultsKey(2022, "SP")).Should().BeTrue();
        TseImport.IsTseUnitKey("IbgeStates").Should().BeFalse();
    }

    [Fact]
    public void YearCatalogIsOrderedAndComplete()
    {
        TseYears.Candidates.Should().BeInAscendingOrder();
        TseYears.Results.Should().BeInAscendingOrder();
        TseYears.Candidates.Min().Should().Be(1994);
        TseYears.Results.Min().Should().Be(1998);
        TseYears.Candidates.Should().Contain(2022);
        TseYears.Results.Should().Contain(2022);
    }
}
