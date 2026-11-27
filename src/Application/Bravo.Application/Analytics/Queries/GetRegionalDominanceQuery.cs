using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record GetRegionalDominanceQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<StateElectoralDominance>>>, ILimitedAnalyticsQuery
{
    public int Limit { get; set; } = 100;
}
