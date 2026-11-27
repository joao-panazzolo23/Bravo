using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class CompareElectionRoundsHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<CompareElectionRoundsQuery, Result<ElectionRoundComparison?>>
{
    public async ValueTask<Result<ElectionRoundComparison?>> Handle(
        CompareElectionRoundsQuery query,
        CancellationToken cancellationToken)
    {
        var comparison = await analytics.CompareElectionRoundsAsync(
            query.ToFilter(), query.BaselineRound, query.ComparedRound, cancellationToken);

        if (comparison is null)
        {
            return Result<ElectionRoundComparison?>.NotFound(
                message: "The two rounds are not comparable for the requested election and candidate.");
        }

        return Result<ElectionRoundComparison?>.OK(comparison);
    }
}
