using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

public sealed class CompareElectionRoundsQueryValidator : AbstractValidator<CompareElectionRoundsQuery>
{
    public CompareElectionRoundsQueryValidator()
    {
        RuleFor(query => query.BaselineRound)
            .GreaterThan((short)0);

        RuleFor(query => query.ComparedRound)
            .GreaterThan((short)0)
            .NotEqual(query => query.BaselineRound);
    }
}
