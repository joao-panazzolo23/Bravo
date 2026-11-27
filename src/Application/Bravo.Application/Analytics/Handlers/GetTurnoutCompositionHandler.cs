using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class GetTurnoutCompositionHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<GetTurnoutCompositionQuery, Result<IReadOnlyList<TurnoutComposition>>>
{
    public async ValueTask<Result<IReadOnlyList<TurnoutComposition>>> Handle(
        GetTurnoutCompositionQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.GetTurnoutCompositionAsync(
            query.ToFilter(), query.Level, query.Limit, cancellationToken);

        return Result<IReadOnlyList<TurnoutComposition>>.OK(rows);
    }
}
