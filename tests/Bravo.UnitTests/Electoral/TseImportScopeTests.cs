using Bravo.Application.Imports.Tse;
using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Models;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

public sealed class TseCatalogTests
{
    [Fact]
    public void YearCatalogsAreOrderedAndComplete()
    {
        TseCatalog.CandidateYears.Should().BeInAscendingOrder();
        TseCatalog.ResultYears.Should().BeInAscendingOrder();

        TseCatalog.CandidateYears.Min().Should().Be(1994);
        TseCatalog.ResultYears.Min().Should().Be(1998);

        TseCatalog.CandidateYears.Should().Contain(2022);
        TseCatalog.ResultYears.Should().Contain(2022);
    }

    [Fact]
    public void ResultsExistOnlyForYearsThatPublishThem()
    {
        TseCatalog.ResultYears.Should().NotContain(1994);
        TseCatalog.ResultYears.Should().NotContain(1996);
    }

    [Fact]
    public void ResultStatesCoverTheTwentySevenStatesPlusExterior()
    {
        TseCatalog.ResultStates.Should().HaveCount(28);
        TseCatalog.ResultStates.Should().Contain("SP");
        TseCatalog.ResultStates.Should().Contain("ZZ");
        TseCatalog.ResultStates.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void OperationCountsMatchTheCatalog()
    {
        TseCatalog.ReferenceOperationCount.Should().Be(TseCatalog.CandidateYears.Length);
        TseCatalog.CandidateOperationCount.Should().Be(TseCatalog.CandidateYears.Length);
        TseCatalog.ResultOperationCount
            .Should().Be(TseCatalog.ResultYears.Length * TseCatalog.ResultStates.Length);
    }
}
