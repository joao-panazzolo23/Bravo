using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class GetRegionalDominanceHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<GetRegionalDominanceQuery, Result<IReadOnlyList<StateElectoralDominance>>>
{
    public async ValueTask<Result<IReadOnlyList<StateElectoralDominance>>> Handle(
        GetRegionalDominanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.GetRegionalDominanceAsync(
            query.ToFilter(), query.Limit, cancellationToken);

        return Result<IReadOnlyList<StateElectoralDominance>>.OK(rows);
    }
}
