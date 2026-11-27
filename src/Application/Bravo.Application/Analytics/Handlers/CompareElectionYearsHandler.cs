using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class CompareElectionYearsHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<CompareElectionYearsQuery, Result<HistoricalPerformanceComparison?>>
{
    public async ValueTask<Result<HistoricalPerformanceComparison?>> Handle(
        CompareElectionYearsQuery query,
        CancellationToken cancellationToken)
    {
        var comparison = await analytics.CompareElectionYearsAsync(
            query.ToFilter(), query.BaselineYear, query.ComparedYear, cancellationToken);

        if (comparison is null)
        {
            return Result<HistoricalPerformanceComparison?>.NotFound(
                message: "The two elections are not comparable for the requested office and candidate.");
        }

        return Result<HistoricalPerformanceComparison?>.OK(comparison);
    }
}
