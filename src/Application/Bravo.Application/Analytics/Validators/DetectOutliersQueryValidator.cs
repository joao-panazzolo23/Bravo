using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

public sealed class DetectOutliersQueryValidator
    : LimitedAnalyticsQueryValidator<DetectOutliersQuery>
{
    public DetectOutliersQueryValidator()
    {
        RuleFor(query => query.Sensitivity)
            .GreaterThan(0m);
    }
}
