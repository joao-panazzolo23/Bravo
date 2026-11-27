using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record RankVoteConcentrationQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<PollingStationVoteConcentration>>>, ILimitedAnalyticsQuery
{
    public int Limit { get; set; } = 100;
}
