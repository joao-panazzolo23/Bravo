using Bravo.Domain.Electoral.Analytics;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Pins the incompatibility rules of the two comparison calculators.
/// </summary>
public sealed class ComparisonCalculatorTests
{
    private static HistoricalElectionPerformance Performance(
        int year,
        string office,
        short round,
        long votes,
        decimal percentage,
        Guid? electionId = null)
        => new(
            year,
            electionId ?? Guid.CreateVersion7(),
            office,
            round,
            Guid.CreateVersion7(),
            "CANDIDATE",
            Guid.CreateVersion7(),
            votes,
            1000,
            percentage,
            5,
            2,
            45m,
            Guid.CreateVersion7(),
            "STRONGEST",
            60m);

    [Fact]
    public void ComparingDifferentOfficesYieldsNoComparison()
    {
        var comparison = YearComparisonCalculator.Compare(
            Performance(2018, "PRESIDENTE", 1, 100, 10m),
            Performance(2022, "GOVERNADOR", 1, 200, 20m),
            [],
            []);

        comparison.Should().BeNull();
    }

    [Fact]
    public void RoundComparisonRejectsDifferentElections()
    {
        var comparison = RoundComparisonCalculator.Compare(
            Performance(2022, "PRESIDENTE", 1, 100, 10m),
            Performance(2022, "PRESIDENTE", 2, 200, 20m),
            [],
            []);

        // Different election identifiers: the rounds belong to different contests.
        comparison.Should().BeNull();
    }

    [Fact]
    public void RoundComparisonSeparatesRelativeAndPercentagePointVariation()
    {
        var electionId = Guid.CreateVersion7();

        var comparison = RoundComparisonCalculator.Compare(
            Performance(2022, "PRESIDENTE", 1, 1000, 40m, electionId),
            Performance(2022, "PRESIDENTE", 2, 1500, 45m, electionId),
            [],
            []);

        comparison.Should().NotBeNull();
        comparison!.VoteVariation.Should().Be(500);
        comparison.VoteVariationPercentage.Should().Be(50m);
        comparison.ShareVariationPercentagePoints.Should().Be(5m);
    }

    [Fact]
    public void YearComparisonKeepsAbsoluteVariationWhenTheBaselineIsZero()
    {
        var comparison = YearComparisonCalculator.Compare(
            Performance(2018, "PRESIDENTE", 1, 0, 0m),
            Performance(2022, "PRESIDENTE", 1, 300, 30m),
            [],
            []);

        comparison.Should().NotBeNull();
        comparison!.VoteVariation.Should().Be(300);
        comparison.VoteVariationPercentage.Should().BeNull();
    }
}
