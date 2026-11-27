using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

public sealed class GetCandidateIdentityHistoryQueryValidator
    : AbstractValidator<GetCandidateIdentityHistoryQuery>
{
    public GetCandidateIdentityHistoryQueryValidator()
    {
        RuleFor(query => query.CandidateIdentityId)
            .NotEmpty();

        RuleFor(query => query.Year)
            .GreaterThan(0)
            .When(query => query.Year is not null);
    }
}
