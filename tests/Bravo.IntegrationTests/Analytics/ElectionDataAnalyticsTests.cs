using Bravo.Domain.Electoral.Analytics;
using Bravo.IntegrationTests.Abstract;
using Bravo.IntegrationTests.Configurations;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Analytics;

/// <summary>
/// Exercises the analytical SQL against a real PostgreSQL instance.
/// </summary>
/// <remarks>
/// The assertions are arithmetic that can be verified by hand from the seed comments, not a
/// snapshot of whatever the query happened to return. That is what makes them meaningful for
/// correctness: valid-vote denominators, round separation, double-counting and division by zero are
/// exactly the properties only a database can prove.
/// </remarks>
[Collection("Database")]
public sealed class ElectionDataAnalyticsTests(PostgresContainerFixture pg) : IntegrationTestBase(pg)
{
    private IElectionDataAnalytics Analytics => _serviceScope.ServiceProvider
        .GetRequiredService<IElectionDataAnalytics>();

    private AsyncServiceScope _serviceScope;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _serviceScope = pg.Factory.Services.CreateAsyncScope();
    }

    [Fact]
    public async Task ThresholdFindsOnlySectionsReachingTheMinimumShare()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "A");
        var filter = RoundOneFilter(ids);

        var page = await Analytics.FindVoteConcentrationAsync(
            filter with { CandidateId = ids.CandidateA2022Id },
            minimumPercentage: 60m,
            maximumPercentage: null,
            page: 1,
            pageSize: 20);

        // 60/100 in section 101 and 50/50 in section 202 reach 60%; 45/90 and 0/150 do not.
        page.Items.Should().Be(2);
        page.Results!.Select(row => row.TseSectionNumber).Should().BeEquivalentTo([101, 202]);
        page.Results!.Should().OnlyContain(row => row.CandidatePercentage >= 60m);
    }

    [Fact]
    public async Task ShareUsesValidVotesAndExcludesBlankAndNullBallots()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "B");
        var filter = RoundOneFilter(ids);

        var page = await Analytics.FindVoteConcentrationAsync(
            filter with { CandidateId = ids.CandidateA2022Id },
            minimumPercentage: 0m,
            maximumPercentage: null,
            page: 1,
            pageSize: 50);

        var section = page.Results!.Single(row => row.TseSectionNumber == 101);

        // 60 of 100 valid (60 nominal + 30 nominal + 10 party); the party vote is valid but is not
        // a rival candidate vote, so opposing stays 30 and the difference is 30.
        section.ValidVotes.Should().Be(100);
        section.CandidateVotes.Should().Be(60);
        section.OpposingVotes.Should().Be(30);
        section.CandidatePercentage.Should().Be(60m);
        section.OpposingPercentage.Should().Be(30m);
        section.DifferencePercentagePoints.Should().Be(30m);
    }

    [Fact]
    public async Task SectionsWithoutValidVotesAreKeptButNeverDivideByZero()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "C");
        var filter = RoundOneFilter(ids);

        var page = await Analytics.FindVoteConcentrationAsync(
            filter with { CandidateId = ids.CandidateA2022Id },
            minimumPercentage: 0m,
            maximumPercentage: null,
            page: 1,
            pageSize: 50);

        var zero = page.Results!.Single(row => row.TseSectionNumber == 103);
        zero.ValidVotes.Should().Be(0);
        zero.CandidatePercentage.Should().Be(0m);
    }

    [Fact]
    public async Task RoundsAreNeverMixed()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "D");

        var roundOne = await Analytics.FindVoteConcentrationAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            0m, null, 1, 50);
        var roundTwo = await Analytics.FindVoteConcentrationAsync(
            RoundTwoFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            0m, null, 1, 50);

        roundOne.Results!.Should().OnlyContain(row => row.Round == 1);
        roundTwo.Results!.Should().OnlyContain(row => row.Round == 2);
        roundTwo.Results!.Should().OnlyContain(row => row.IsRunoff);
    }

    [Fact]
    public async Task RankingIsOrderedByShareAndRespectsTheLimit()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "E");

        var ranking = await Analytics.RankVoteConcentrationAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            limit: 2);

        ranking.Should().HaveCount(2);
        ranking[0].CandidatePercentage.Should().Be(100m);
        ranking[1].CandidatePercentage.Should().Be(60m);
        ranking.Should().BeInDescendingOrder(row => row.CandidatePercentage);
    }

    [Fact]
    public async Task MunicipalityPerformanceAggregatesSectionsWithoutDoubleCounting()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "F");

        var performance = await Analytics.GetMunicipalityPerformanceAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            MunicipalityPerformanceSort.VotePercentage,
            limit: 10);

        var one = performance.Single(row => row.MunicipalityId == ids.MunicipalityOneId);
        var two = performance.Single(row => row.MunicipalityId == ids.MunicipalityTwoId);

        // Municipality ONE: A=105 of 200 valid (100 + 90 + 10 blank... blank is not valid, so 190).
        one.Votes.Should().Be(105);
        one.ValidVotes.Should().Be(190);
        one.RankAmongCandidates.Should().Be(1);

        // Municipality TWO: A=50 of 200 valid, second behind B (150).
        two.Votes.Should().Be(50);
        two.ValidVotes.Should().Be(200);
        two.RankAmongCandidates.Should().Be(2);
    }

    [Fact]
    public async Task MarginsUseTheTwoStrongestCandidates()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "G");

        var closest = await Analytics.GetMunicipalityMarginsAsync(
            RoundOneFilter(ids), MarginDirection.Closest, limit: 10);

        var one = closest.Single(row => row.MunicipalityId == ids.MunicipalityOneId);

        one.WinnerName.Should().Be("CANDIDATO A");
        one.WinnerVotes.Should().Be(105);
        one.RunnerUpName.Should().Be("CANDIDATO B");
        one.RunnerUpVotes.Should().Be(75);
        one.MarginVotes.Should().Be(30);
    }

    [Fact]
    public async Task TurnoutReportsBlankAndNullSharesAndLeavesParticipationNull()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "H");

        var turnout = await Analytics.GetTurnoutCompositionAsync(
            RoundOneFilter(ids),
            VoteCompositionLevel.Municipality,
            limit: 10);

        var one = turnout.Single(row => row.MunicipalityId == ids.MunicipalityOneId);

        // Votes cast in ONE: 60+30+10 party+45+45+10 blank+5 null = 205; valid = 190.
        one.VotesCast.Should().Be(205);
        one.ValidVotes.Should().Be(190);
        one.BlankVotes.Should().Be(10);
        one.NullVotes.Should().Be(5);

        // No eligible-voter source exists, so participation must not be fabricated.
        one.EligibleVoters.Should().BeNull();
        one.TurnoutPercentage.Should().BeNull();
        one.AbsenteeismPercentage.Should().BeNull();
        one.BlankVotePercentage.Should().Be(4.88m);
    }

    [Fact]
    public async Task OutliersCompareASectionWithItsOwnMunicipality()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "I");

        var outliers = await Analytics.DetectOutliersAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            OutlierReferenceLevel.Municipality,
            sensitivity: 1m,
            limit: 50);

        outliers.Should().NotBeEmpty();
        outliers.Should().OnlyContain(row => row.DeviationRatio != null || row.ReferenceSectionCount < 2);
        outliers.Should().OnlyContain(row => row.ReferenceSectionCount >= 2);
    }

    [Fact]
    public async Task DominanceCountsMunicipalitiesWonStrictly()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "J");

        var dominance = await Analytics.GetRegionalDominanceAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            limit: 10);

        var state = dominance.Single();

        // A wins ONE (105 > 75) and loses TWO (50 < 150).
        state.MunicipalitiesContested.Should().Be(2);
        state.MunicipalitiesWon.Should().Be(1);
        state.TotalVotes.Should().Be(155);
        state.ValidVotes.Should().Be(390);
    }

    [Fact]
    public async Task RoundComparisonKeepsRelativeAndPercentagePointVariationApart()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "K");

        var comparison = await Analytics.CompareElectionRoundsAsync(
            new ElectionAnalyticsFilter(
                ElectionId: ids.Election2022Id,
                OfficeId: ids.OfficeId,
                CandidateId: ids.CandidateA2022Id),
            baselineRound: 1,
            comparedRound: 2);

        comparison.Should().NotBeNull();
        // Round 1: 155 of 390; round 2: 200 of 260.
        comparison!.BaselineVotes.Should().Be(155);
        comparison.ComparedVotes.Should().Be(200);
        comparison.VoteVariation.Should().Be(45);
        comparison.ShareVariationPercentagePoints.Should().Be(
            Math.Round(200m * 100m / 260m - 155m * 100m / 390m, 2));
    }

    [Fact]
    public async Task YearComparisonRejectsADifferentOfficeRequest()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "L");

        var comparison = await Analytics.CompareElectionYearsAsync(
            new ElectionAnalyticsFilter(
                OfficeId: ids.OfficeId,
                CandidateId: ids.CandidateA2022Id),
            baselineYear: 2018,
            comparedYear: 2022);

        // Candidate A 2022 did not contest 2018 under the same identifier, so there is no
        // comparable pair; the service reports that instead of inventing a link.
        comparison.Should().BeNull();
    }

    [Fact]
    public async Task IdentityHistorySpansCandidaciesWithoutAssumingAParty()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "M");

        var history = await Analytics.GetCandidateIdentityHistoryAsync(
            ids.IdentityId, year: null, officeId: null);

        history.Should().NotBeNull();
        history!.FullName.Should().Be("IDENTIDADE COMPARTILHADA");
        history.YearsContested.Should().BeEquivalentTo([2018, 2022]);
        history.OfficesContested.Should().Equal("CARGO TESTE");
        history.Candidacies.Should().NotBeEmpty();

        // The history is filtered by identity, but each candidacy must still name the candidate
        // registration it came from; a null name here would lose the contest context.
        history.Candidacies.Should().OnlyContain(candidacy => candidacy.CandidateName != null);
        history.Candidacies.Should().OnlyContain(candidacy => candidacy.CandidateId != null);
    }

    [Fact]
    public async Task InactiveDatasetVersionsAreIgnored()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "N");

        await Db.ExecuteAsync(
            """UPDATE "DatasetVersions" SET "IsActive" = false WHERE "Id" = @id;""",
            new { id = ids.ActiveVersionId });

        var page = await Analytics.FindVoteConcentrationAsync(
            RoundOneFilter(ids) with { CandidateId = ids.CandidateA2022Id },
            0m, null, 1, 50);

        page.Items.Should().Be(0);
        page.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyResultSetIsNotAnError()
    {
        var ids = await AnalyticsSeed.CreateAsync(Db, "O");

        var page = await Analytics.FindVoteConcentrationAsync(
            RoundOneFilter(ids) with { CandidateId = Guid.CreateVersion7() },
            minimumPercentage: 0m,
            maximumPercentage: null,
            page: 1,
            pageSize: 10);

        page.Items.Should().Be(0);
        page.Results.Should().BeEmpty();
        page.Count.Should().Be(0);
    }

    private static ElectionAnalyticsFilter RoundOneFilter(AnalyticsSeedIds ids) => new(
        Year: 2022,
        ElectionId: ids.Election2022Id,
        OfficeId: ids.OfficeId,
        Round: 1);

    private static ElectionAnalyticsFilter RoundTwoFilter(AnalyticsSeedIds ids) => new(
        Year: 2022,
        ElectionId: ids.Election2022Id,
        OfficeId: ids.OfficeId,
        Round: 2);
}
