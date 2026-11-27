using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record GetMunicipalityPerformanceQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<MunicipalityCandidatePerformance>>>, ILimitedAnalyticsQuery
{
    public MunicipalityPerformanceSort Sort { get; set; } = MunicipalityPerformanceSort.VotePercentage;
    public int Limit { get; set; } = 100;
}
