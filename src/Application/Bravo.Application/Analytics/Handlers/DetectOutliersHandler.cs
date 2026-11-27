using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class DetectOutliersHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<DetectOutliersQuery, Result<IReadOnlyList<SectionVoteOutlier>>>
{
    public async ValueTask<Result<IReadOnlyList<SectionVoteOutlier>>> Handle(
        DetectOutliersQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.DetectOutliersAsync(
            query.ToFilter(), query.ReferenceLevel, query.Sensitivity, query.Limit, cancellationToken);

        return Result<IReadOnlyList<SectionVoteOutlier>>.OK(rows);
    }
}
