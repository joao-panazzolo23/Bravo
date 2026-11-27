using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record DetectOutliersQuery : ElectionAnalyticsQuery, IQuery<Result<IReadOnlyList<SectionVoteOutlier>>>, ILimitedAnalyticsQuery
{
    public OutlierReferenceLevel ReferenceLevel { get; set; } = OutlierReferenceLevel.Municipality;
    public decimal Sensitivity { get; set; } = 3m;
    public int Limit { get; set; } = 100;
}
