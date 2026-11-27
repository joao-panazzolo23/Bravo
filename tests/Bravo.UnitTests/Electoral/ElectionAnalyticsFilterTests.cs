using Bravo.Domain.Electoral.Analytics;
using FluentAssertions;

namespace Bravo.UnitTests.Electoral;

/// <summary>
/// Pins the filter contract every analysis relies on to keep rounds and offices apart.
/// </summary>
public sealed class ElectionAnalyticsFilterTests
{
    [Fact]
    public void RequireOfficeRejectsAConflictingCandidateAndIdentity()
    {
        var filter = new ElectionAnalyticsFilter(
            OfficeId: Guid.CreateVersion7(),
            CandidateId: Guid.CreateVersion7(),
            CandidateIdentityId: Guid.CreateVersion7());

        var act = () => filter.RequireOffice();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireCandidateRequiresEitherACandidateOrAnIdentity()
    {
        var filter = new ElectionAnalyticsFilter(
            OfficeId: Guid.CreateVersion7(),
            Round: 1);

        var act = () => filter.RequireCandidate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireContestNeedsAYearOrAnElectionToBoundTheScan()
    {
        var filter = new ElectionAnalyticsFilter(
            OfficeId: Guid.CreateVersion7(),
            Round: 1);

        var act = () => filter.RequireContest();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireContestAcceptsAYearWithAnOfficeAndRound()
    {
        var filter = new ElectionAnalyticsFilter(
            Year: 2022,
            OfficeId: Guid.CreateVersion7(),
            Round: 2);

        filter.RequireContest().Should().BeSameAs(filter);
    }

    [Fact]
    public void RequireElectionRejectsAMissingElection()
    {
        new ElectionAnalyticsFilter(OfficeId: Guid.CreateVersion7())
            .Invoking(filter => filter.RequireElection())
            .Should().Throw<ArgumentException>();
    }
}
