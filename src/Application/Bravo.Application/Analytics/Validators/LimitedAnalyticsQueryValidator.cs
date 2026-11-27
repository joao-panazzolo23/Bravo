using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

/// <summary>
/// Shared rules for analyses that return a bounded list.
/// </summary>
public abstract class LimitedAnalyticsQueryValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : ILimitedAnalyticsQuery
{
    protected LimitedAnalyticsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 10_000);
    }
}
