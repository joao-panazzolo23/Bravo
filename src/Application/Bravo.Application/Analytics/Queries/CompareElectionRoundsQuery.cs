using Bravo.Domain.Electoral.Analytics;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Analytics.Queries;

public sealed record CompareElectionRoundsQuery : ElectionAnalyticsQuery, IQuery<Result<ElectionRoundComparison?>>
{
    public short BaselineRound { get; set; } = 1;
    public short ComparedRound { get; set; } = 2;
}
