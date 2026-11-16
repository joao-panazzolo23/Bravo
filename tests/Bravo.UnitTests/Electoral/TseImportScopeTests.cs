using Bravo.Application.Importing;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

public sealed class TseImportScopeTests
{
    [Fact]
    public void BuildsAndParsesAllThreeStageKeys()
    {
        var keys = TseImport.KeysFor(2022, "ac", 1, 546);

        keys.Should().HaveCount(3);

        foreach (var key in keys)
        {
            TseImport.TryParse(key, out var stage, out var year, out var state, out var round, out var electionCode)
                .Should().BeTrue();

            year.Should().Be(2022);
            state.Should().Be("AC");
            round.Should().Be(1);
            electionCode.Should().Be(546);
            stage.Should().StartWith("Tse");
        }
    }

    [Fact]
    public void PreservesStageIdentity()
    {
        var scope = TseImportScope.Parse(TseImport.KeysFor(2022, "AC", 2, 547)[2]);

        scope.Stage.Should().Be(TseImport.ResultsStage);
        scope.Round.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TseResults")]
    [InlineData("TseResults:2022:AC")]
    [InlineData("TseResults:abc:AC:1:546")]
    public void RejectsMalformedKeys(string key)
    {
        TseImport.TryParse(key, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void RecognisesItsOwnKeys()
    {
        TseImport.IsTseUnitKey(TseImport.KeysFor(2022, "AC", 1, 546)[0]).Should().BeTrue();
        TseImport.IsTseUnitKey("IbgeStates").Should().BeFalse();
    }
}
