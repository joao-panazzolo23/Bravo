using Bravo.Domain.Electoral.Analytics;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Pins the percentage-vs-percentage-point distinction used by every comparison.
/// </summary>
public sealed class VariationCalculatorTests
{
    [Fact]
    public void RelativeVariationComparesTheDifferenceWithTheBaseline()
    {
        // 120 against 100 is a 20% growth in the count.
        VariationCalculator.Relative(20, 100).Should().Be(20m);
    }

    [Fact]
    public void RelativeVariationIsNullWhenTheBaselineIsZero()
    {
        // Growing from zero has no ratio; the absolute difference is the only honest number.
        VariationCalculator.Relative(50, 0).Should().BeNull();
    }

    [Fact]
    public void RelativeVariationKeepsTheSignOfADecline()
    {
        VariationCalculator.Relative(-25, 100).Should().Be(-25m);
    }

    [Fact]
    public void ShareVariationIsAPercentagePointDifferenceNotARelativeChange()
    {
        // 50% to 40% is ten percentage points, not a 20% drop.
        var baseline = new VoteShare(500, 50m);
        var compared = new VoteShare(400, 40m);

        (baseline.Percentage - compared.Percentage).Should().Be(10m);
        VariationCalculator.Relative(compared.Votes - baseline.Votes, baseline.Votes)
            .Should().Be(-20m);
    }
}
