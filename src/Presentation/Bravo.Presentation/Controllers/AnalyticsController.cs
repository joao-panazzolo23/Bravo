using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Presentation.Authorization;
using Bravo.Shared.Pagination;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bravo.Presentation.Controllers;

/// <summary>
/// Read-only electoral analytics exposed over HTTP.
/// </summary>
/// <remarks>
/// This controller only forwards requests to the Application layer; every filter, validation and
/// calculation belongs to the analytics pipeline. Responses are wrapped in <see cref="Result{T}"/>
/// exactly like the other endpoints, so the HTTP status and the payload stay consistent.
/// </remarks>
[ApiController]
[Route("api/v1/analytics")]
[Authorize(Roles = AuthorizationRoles.Administrators)]
public class AnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpGet("vote-concentration")]
    public async Task<ActionResult<Result<PaginatedList<PollingStationVoteConcentration>>>> FindVoteConcentration(
        [FromQuery] FindVoteConcentrationQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("vote-concentration/ranking")]
    public async Task<ActionResult<Result<IReadOnlyList<PollingStationVoteConcentration>>>> RankVoteConcentration(
        [FromQuery] RankVoteConcentrationQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("municipalities/performance")]
    public async Task<ActionResult<Result<IReadOnlyList<MunicipalityCandidatePerformance>>>> GetMunicipalityPerformance(
        [FromQuery] GetMunicipalityPerformanceQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("municipalities/margins")]
    public async Task<ActionResult<Result<IReadOnlyList<MunicipalityElectoralMargin>>>> GetMunicipalityMargins(
        [FromQuery] GetMunicipalityMarginsQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("municipalities/dominance")]
    public async Task<ActionResult<Result<IReadOnlyList<StateElectoralDominance>>>> GetRegionalDominance(
        [FromQuery] GetRegionalDominanceQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("turnout")]
    public async Task<ActionResult<Result<IReadOnlyList<TurnoutComposition>>>> GetTurnoutComposition(
        [FromQuery] GetTurnoutCompositionQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("outliers")]
    public async Task<ActionResult<Result<IReadOnlyList<SectionVoteOutlier>>>> DetectOutliers(
        [FromQuery] DetectOutliersQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("comparison/years")]
    public async Task<ActionResult<Result<HistoricalPerformanceComparison?>>> CompareElectionYears(
        [FromQuery] CompareElectionYearsQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("comparison/rounds")]
    public async Task<ActionResult<Result<ElectionRoundComparison?>>> CompareElectionRounds(
        [FromQuery] CompareElectionRoundsQuery query)
    {
        var response = await mediator.Send(query);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("candidate-identities/{candidateIdentityId:guid}/history")]
    public async Task<ActionResult<Result<CandidateIdentityHistory?>>> GetCandidateIdentityHistory(
        [FromRoute] Guid candidateIdentityId,
        [FromQuery] int? year,
        [FromQuery] Guid? officeId)
    {
        var response = await mediator.Send(
            new GetCandidateIdentityHistoryQuery
            {
                CandidateIdentityId = candidateIdentityId,
                Year = year,
                OfficeId = officeId
            });

        return StatusCode(response.StatusCode, response);
    }
}
