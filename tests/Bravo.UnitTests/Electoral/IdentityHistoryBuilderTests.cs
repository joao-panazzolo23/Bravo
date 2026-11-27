using Bravo.Domain.Electoral.Analytics;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Pins the identity history projection, including the absence of any party-level inference.
/// </summary>
public sealed class IdentityHistoryBuilderTests
{
    private static HistoricalElectionPerformance Performance(
        int year,
        short round,
        string office,
        long votes,
        decimal percentage)
        => new(
            year,
            Guid.CreateVersion7(),
            office,
            round,
            Guid.CreateVersion7(),
            "CANDIDATE",
            null,
            votes,
            1000,
            percentage,
            1,
            0,
            20m,
            null,
            null,
            null);

    [Fact]
    public void CandidaciesAreOrderedAndSummarised()
    {
        var identityId = Guid.CreateVersion7();

        var history = IdentityHistoryBuilder.Build(
            identityId,
            "FULL NAME",
            [
                Performance(2022, 1, "PRESIDENTE", 300, 30m),
                Performance(2018, 1, "PRESIDENTE", 100, 10m),
                Performance(2022, 2, "PRESIDENTE", 400, 40m)
            ]);

        history.CandidateIdentityId.Should().Be(identityId);
        history.YearsContested.Should().Equal(2018, 2022);
        history.OfficesContested.Should().Equal("PRESIDENTE");
        history.LifetimeVotes.Should().Be(800);
        history.Candidacies.Select(candidacy => candidacy.Year)
            .Should().Equal(2018, 2022, 2022);
    }

    [Fact]
    public void AnIdentityWithoutCandidaciesStillProducesAnEmptyHistory()
    {
        var history = IdentityHistoryBuilder.Build(
            Guid.CreateVersion7(),
            "FULL NAME",
            []);

        history.Candidacies.Should().BeEmpty();
        history.YearsContested.Should().BeEmpty();
        history.LifetimeVotes.Should().Be(0);
    }
}
