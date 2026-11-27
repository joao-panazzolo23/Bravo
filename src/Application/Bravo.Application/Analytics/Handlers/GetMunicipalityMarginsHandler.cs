using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class GetMunicipalityMarginsHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<GetMunicipalityMarginsQuery, Result<IReadOnlyList<MunicipalityElectoralMargin>>>
{
    public async ValueTask<Result<IReadOnlyList<MunicipalityElectoralMargin>>> Handle(
        GetMunicipalityMarginsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await analytics.GetMunicipalityMarginsAsync(
            query.ToFilter(), query.Direction, query.Limit, cancellationToken);

        return Result<IReadOnlyList<MunicipalityElectoralMargin>>.OK(rows);
    }
}
