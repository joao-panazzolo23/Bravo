using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

public sealed class CompareElectionYearsQueryValidator : AbstractValidator<CompareElectionYearsQuery>
{
    public CompareElectionYearsQueryValidator()
    {
        RuleFor(query => query.BaselineYear)
            .GreaterThan(0);

        RuleFor(query => query.ComparedYear)
            .GreaterThan(0)
            .NotEqual(query => query.BaselineYear);
    }
}
