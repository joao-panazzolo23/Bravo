using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record GetTurnoutCompositionQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<TurnoutComposition>>>, ILimitedAnalyticsQuery
{
    public VoteCompositionLevel Level { get; set; } = VoteCompositionLevel.Municipality;
    public int Limit { get; set; } = 100;
}
