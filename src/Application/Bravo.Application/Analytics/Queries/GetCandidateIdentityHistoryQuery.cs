using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record GetCandidateIdentityHistoryQuery : IQuery<Result<CandidateIdentityHistory?>>
{
    public Guid CandidateIdentityId { get; set; }
    public int? Year { get; set; }
    public Guid? OfficeId { get; set; }
}
