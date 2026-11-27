using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

/// <summary>
/// Sections where a candidate or identity reached a share of valid votes inside a window.
/// </summary>
/// <example>
/// <c>new FindVoteConcentrationQuery { Year = 2022, OfficeId = officeId, Round = 2, MinimumPercentage = 90m, Page = 1, PageSize = 50 }</c>
/// </example>
public sealed record FindVoteConcentrationQuery : ElectionAnalyticsQuery, IQuery<Result<PaginatedList<PollingStationVoteConcentration>>>
{
    public decimal MinimumPercentage { get; set; }
    public decimal? MaximumPercentage { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
