using Bravo.Application.Analytics.Queries;
using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Handlers;

public sealed class GetCandidateIdentityHistoryHandler(IElectionDataAnalytics analytics)
    : IQueryHandler<GetCandidateIdentityHistoryQuery, Result<CandidateIdentityHistory?>>
{
    public async ValueTask<Result<CandidateIdentityHistory?>> Handle(
        GetCandidateIdentityHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var history = await analytics.GetCandidateIdentityHistoryAsync(
            query.CandidateIdentityId, query.Year, query.OfficeId, cancellationToken);

        if (history is null)
        {
            return Result<CandidateIdentityHistory?>.NotFound(
                message: "Candidate identity not found or has no candidacies.");
        }

        return Result<CandidateIdentityHistory?>.OK(history);
    }
}
