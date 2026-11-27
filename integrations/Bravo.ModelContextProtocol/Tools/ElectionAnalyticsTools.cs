using System.ComponentModel;
using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;
using Mediator;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bravo.ModelContextProtocol.Tools;

/// <summary>
/// Analytical tools over the stored electoral results.
/// </summary>
/// <remarks>
/// Each tool is a thin adapter over one Application query, so an MCP client and an HTTP client
/// receive the same calculation and the same validation. Tools return the domain records directly:
/// they are read projections with no persistence types, which the MCP serializer can render as
/// JSON without a mapping layer.
///
/// Filters are passed as flat optional parameters instead of an object because MCP clients present
/// tool arguments as a flat form; the helper at the bottom maps them to
/// <see cref="ElectionAnalyticsFilter"/>. Office and round are still required by the domain service
/// for share-based analyses, and that requirement surfaces as a tool error rather than a silently
/// wrong denominator.
/// </remarks>
[McpServerToolType]
public sealed class ElectionAnalyticsTools(IMediator mediator)
{
    [McpServerTool(Name = "find_vote_concentration")]
    [Description("Sections where a candidate reached a share of valid votes inside a percentage window, separated by round.")]
    public async Task<PaginatedList<PollingStationVoteConcentration>> FindVoteConcentration(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID). Required to define the valid-vote denominator.")] Guid officeId,
        [Description("Election round: 1 for the first round, 2 for the runoff.")] short round,
        [Description("Candidate identifier (GUID). Provide this or candidateIdentityId, not both.")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID). Provide this or candidateId, not both.")] Guid? candidateIdentityId = null,
        [Description("Minimum share of valid votes, 0-100.")] decimal minimumPercentage = 0m,
        [Description("Optional maximum share of valid votes, 0-100.")] decimal? maximumPercentage = null,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional municipality identifier (GUID).")] Guid? municipalityId = null,
        [Description("Optional electoral zone identifier (GUID).")] Guid? zoneId = null,
        [Description("Optional election identifier (GUID) to disambiguate from the year.")] Guid? electionId = null,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Page size, 1-1000.")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new FindVoteConcentrationQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, municipalityId, zoneId, electionId),
                MinimumPercentage = minimumPercentage,
                MaximumPercentage = maximumPercentage,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "rank_vote_concentration")]
    [Description("Sections ordered by a candidate's share of valid votes, highest first.")]
    public async Task<IReadOnlyList<PollingStationVoteConcentration>> RankVoteConcentration(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Maximum number of sections to return.")] int limit = 100,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional municipality identifier (GUID).")] Guid? municipalityId = null,
        [Description("Optional electoral zone identifier (GUID).")] Guid? zoneId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new RankVoteConcentrationQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, municipalityId, zoneId, electionId),
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "get_municipality_performance")]
    [Description("A candidate's performance per municipality in one round, ranked by share or vote count.")]
    public async Task<IReadOnlyList<MunicipalityCandidatePerformance>> GetMunicipalityPerformance(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Sort order: VotePercentage or VoteCount.")] MunicipalityPerformanceSort sort = MunicipalityPerformanceSort.VotePercentage,
        [Description("Maximum number of municipalities to return.")] int limit = 100,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new GetMunicipalityPerformanceQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, null, null, electionId),
                Sort = sort,
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "get_municipality_margins")]
    [Description("Municipalities ordered by the margin between their two strongest candidates.")]
    public async Task<IReadOnlyList<MunicipalityElectoralMargin>> GetMunicipalityMargins(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Direction: Closest or Largest.")] MarginDirection direction = MarginDirection.Closest,
        [Description("Maximum number of municipalities to return.")] int limit = 100,
        [Description("Optional candidate identifier (GUID) to keep only municipalities it led or contested in the top two.")] Guid? candidateId = null,
        [Description("Optional candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new GetMunicipalityMarginsQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, null, null, electionId),
                Direction = direction,
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "get_regional_dominance")]
    [Description("Dominance of a candidate per state: municipalities contested and won, average and extreme shares.")]
    public async Task<IReadOnlyList<StateElectoralDominance>> GetRegionalDominance(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Maximum number of states to return.")] int limit = 100,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new GetRegionalDominanceQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, null, null, electionId),
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "get_turnout_composition")]
    [Description("Vote composition at a geographic level. Participation fields are null because no eligible-voter source is stored.")]
    public async Task<IReadOnlyList<TurnoutComposition>> GetTurnoutComposition(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Aggregation level: PollingStation, Municipality, ElectoralZone or State.")] VoteCompositionLevel level = VoteCompositionLevel.Municipality,
        [Description("Maximum number of rows to return.")] int limit = 100,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional municipality identifier (GUID).")] Guid? municipalityId = null,
        [Description("Optional electoral zone identifier (GUID).")] Guid? zoneId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new GetTurnoutCompositionQuery
            {
                Filter = Filter(year, officeId, round, null, null, stateId, municipalityId, zoneId, electionId),
                Level = level,
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "detect_outliers")]
    [Description("Sections whose candidate share deviates from its reference population. A deviation is statistical only, never a fraud finding.")]
    public async Task<IReadOnlyList<SectionVoteOutlier>> DetectOutliers(
        [Description("Election year, for example 2022.")] int year,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Election round: 1 or 2.")] short round,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Reference population: Municipality, ElectoralZone or State.")] OutlierReferenceLevel referenceLevel = OutlierReferenceLevel.Municipality,
        [Description("Sensitivity in standard deviations; must be greater than zero.")] decimal sensitivity = 3m,
        [Description("Maximum number of sections to return.")] int limit = 100,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Optional municipality identifier (GUID).")] Guid? municipalityId = null,
        [Description("Optional election identifier (GUID).")] Guid? electionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new DetectOutliersQuery
            {
                Filter = Filter(year, officeId, round, candidateId, candidateIdentityId, stateId, municipalityId, null, electionId),
                ReferenceLevel = referenceLevel,
                Sensitivity = sensitivity,
                Limit = limit
            },
            cancellationToken));

        return result.Data!;
    }

    [McpServerTool(Name = "compare_election_years")]
    [Description("Compares one candidate's performance between two election years of the same office.")]
    public async Task<HistoricalPerformanceComparison> CompareElectionYears(
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Baseline election year.")] int baselineYear,
        [Description("Compared election year.")] int comparedYear,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        [Description("Election round to compare, since rounds are separate contests.")] short? round = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new CompareElectionYearsQuery
            {
                Filter = new ElectionAnalyticsFilter(
                    OfficeId: officeId,
                    Round: round,
                    CandidateId: candidateId,
                    CandidateIdentityId: candidateIdentityId,
                    StateId: stateId),
                BaselineYear = baselineYear,
                ComparedYear = comparedYear
            },
            cancellationToken));

        if (result.Data is null)
        {
            throw new McpException(
                "The two elections are not comparable for the requested office and candidate. " +
                "The office must be the same in both years and the candidate must have a registration in each of them.");
        }

        return result.Data;
    }

    [McpServerTool(Name = "compare_election_rounds")]
    [Description("Compares a candidate's performance between two rounds of the same election.")]
    public async Task<ElectionRoundComparison> CompareElectionRounds(
        [Description("Election identifier (GUID).")] Guid electionId,
        [Description("Electoral office identifier (GUID).")] Guid officeId,
        [Description("Baseline round.")] short baselineRound,
        [Description("Compared round.")] short comparedRound,
        [Description("Candidate identifier (GUID).")] Guid? candidateId = null,
        [Description("Candidate identity identifier (GUID).")] Guid? candidateIdentityId = null,
        [Description("Optional state identifier (GUID).")] Guid? stateId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new CompareElectionRoundsQuery
            {
                Filter = new ElectionAnalyticsFilter(
                    ElectionId: electionId,
                    OfficeId: officeId,
                    CandidateId: candidateId,
                    CandidateIdentityId: candidateIdentityId,
                    StateId: stateId),
                BaselineRound = baselineRound,
                ComparedRound = comparedRound
            },
            cancellationToken));

        if (result.Data is null)
        {
            throw new McpException(
                "The two rounds are not comparable for the requested election, office and candidate. " +
                "They must belong to the same election and office, and the candidate must have a registration in both rounds.");
        }

        return result.Data;
    }

    [McpServerTool(Name = "get_candidate_identity_history")]
    [Description("Every candidacy of one candidate identity across elections, with votes and shares per round.")]
    public async Task<CandidateIdentityHistory?> GetCandidateIdentityHistory(
        [Description("Candidate identity identifier (GUID).")] Guid candidateIdentityId,
        [Description("Optional election year to filter the history.")] int? year = null,
        [Description("Optional electoral office identifier (GUID) to filter the history.")] Guid? officeId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ToolFailure.WrapAsync(() => mediator.Send(
            new GetCandidateIdentityHistoryQuery
            {
                CandidateIdentityId = candidateIdentityId,
                Year = year,
                OfficeId = officeId
            },
            cancellationToken));

        return result.Data;
    }

    private static ElectionAnalyticsFilter Filter(
        int year,
        Guid officeId,
        short round,
        Guid? candidateId,
        Guid? candidateIdentityId,
        Guid? stateId,
        Guid? municipalityId,
        Guid? zoneId,
        Guid? electionId)
        => new(
            Year: year,
            ElectionId: electionId,
            OfficeId: officeId,
            Round: round,
            CandidateId: candidateId,
            CandidateIdentityId: candidateIdentityId,
            StateId: stateId,
            MunicipalityId: municipalityId,
            ZoneId: zoneId);
}
