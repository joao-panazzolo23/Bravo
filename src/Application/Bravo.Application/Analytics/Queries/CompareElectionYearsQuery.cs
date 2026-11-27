using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record CompareElectionYearsQuery : ElectionAnalyticsQuery, IQuery<Result<HistoricalPerformanceComparison?>>
{
    public int BaselineYear { get; set; }
    public int ComparedYear { get; set; }
}
