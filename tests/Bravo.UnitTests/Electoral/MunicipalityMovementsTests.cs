using Bravo.Domain.Electoral.Analytics;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Pins the municipality movement rules the year and round comparisons share.
/// </summary>
public sealed class MunicipalityMovementsTests
{
    private static MunicipalityContestStat Stat(
        decimal percentage,
        bool won = false,
        Guid? id = null,
        long validVotes = 100,
        long targetVotes = 50)
        => new(id ?? Guid.CreateVersion7(), validVotes, targetVotes, percentage, won);

    [Fact]
    public void OnlyMunicipalitiesPresentInBothScopesAreCompared()
    {
        var shared = Guid.CreateVersion7();
        var baseline = new[] { Stat(40m, id: shared), Stat(30m) };
        var compared = new[] { Stat(45m, id: shared), Stat(70m) };

        var movements = MunicipalityMovements.Compare(
            baseline, compared, stat => stat.Percentage ?? 0m);

        movements.Compared.Should().Be(1);
        movements.Improved.Should().ContainSingle().Which.Should().Be(shared);
        movements.Declined.Should().BeEmpty();
    }

    [Fact]
    public void ADropInShareIsADeclineAndAnEqualShareIsUnchanged()
    {
        var declined = Guid.CreateVersion7();
        var unchanged = Guid.CreateVersion7();

        var baseline = new[] { Stat(55m, id: declined), Stat(20m, id: unchanged) };
        var compared = new[] { Stat(50m, id: declined), Stat(20m, id: unchanged) };

        var movements = MunicipalityMovements.Compare(
            baseline, compared, stat => stat.Percentage ?? 0m);

        movements.Declined.Should().ContainSingle().Which.Should().Be(declined);
        movements.Unchanged.Should().Be(1);
    }

    [Fact]
    public void RankShiftsTrackFirstPlaceOnly()
    {
        var gained = Guid.CreateVersion7();
        var lost = Guid.CreateVersion7();
        var stayed = Guid.CreateVersion7();

        var baseline = new[]
        {
            Stat(30m, won: false, id: gained),
            Stat(60m, won: true, id: lost),
            Stat(50m, won: true, id: stayed)
        };
        var compared = new[]
        {
            Stat(55m, won: true, id: gained),
            Stat(40m, won: false, id: lost),
            Stat(52m, won: true, id: stayed)
        };

        var shifts = MunicipalityMovements.RankShifts(baseline, compared);

        shifts.Gained.Should().ContainSingle().Which.Should().Be(gained);
        shifts.Lost.Should().ContainSingle().Which.Should().Be(lost);
    }
}
