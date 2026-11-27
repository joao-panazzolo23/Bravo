using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Pagination;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class FindVoteConcentrationHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<FindVoteConcentrationQuery, Result<PaginatedList<PollingStationVoteConcentration>>>
{
    public async ValueTask<Result<PaginatedList<PollingStationVoteConcentration>>> Handle(
        FindVoteConcentrationQuery query,
        CancellationToken cancellationToken)
    {
        var page = await analytics.FindVoteConcentrationAsync(
            query.ToFilter(),
            query.MinimumPercentage,
            query.MaximumPercentage,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Result<PaginatedList<PollingStationVoteConcentration>>.OK(page);
    }
}
