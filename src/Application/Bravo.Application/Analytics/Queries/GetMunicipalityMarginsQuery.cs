using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record GetMunicipalityMarginsQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<MunicipalityElectoralMargin>>>, ILimitedAnalyticsQuery
{
    public MarginDirection Direction { get; set; } = MarginDirection.Closest;
    public int Limit { get; set; } = 100;
}
