using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class GetMunicipalityPerformanceHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<GetMunicipalityPerformanceQuery, Result<IReadOnlyList<MunicipalityCandidatePerformance>>>
{
    public async ValueTask<Result<IReadOnlyList<MunicipalityCandidatePerformance>>> Handle(
        GetMunicipalityPerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.GetMunicipalityPerformanceAsync(
            query.ToFilter(), query.Sort, query.Limit, cancellationToken);

        return Result<IReadOnlyList<MunicipalityCandidatePerformance>>.OK(rows);
    }
}
