using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class RankVoteConcentrationHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<RankVoteConcentrationQuery, Result<IReadOnlyList<PollingStationVoteConcentration>>>
{
    public async ValueTask<Result<IReadOnlyList<PollingStationVoteConcentration>>> Handle(
        RankVoteConcentrationQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.RankVoteConcentrationAsync(
            query.ToFilter(), query.Limit, cancellationToken);

        return Result<IReadOnlyList<PollingStationVoteConcentration>>.OK(rows);
    }
}
