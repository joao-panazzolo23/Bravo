using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

/// <summary>
/// Validates the shared analytical request shape before it reaches the domain service.
/// </summary>
/// <remarks>
/// The domain service still enforces its own invariants (an office is required to compute shares,
/// rounds cannot be mixed). These rules only reject shapes that are wrong regardless of the data,
/// such as an inverted percentage window or a page size that would materialise unbounded rows.
/// </remarks>
public sealed class FindVoteConcentrationQueryValidator : AbstractValidator<FindVoteConcentrationQuery>
{
    public FindVoteConcentrationQueryValidator()
    {
        RuleFor(query => query.MinimumPercentage)
            .InclusiveBetween(0m, 100m);

        RuleFor(query => query.MaximumPercentage)
            .InclusiveBetween(0m, 100m)
            .GreaterThanOrEqualTo(query => query.MinimumPercentage)
            .When(query => query.MaximumPercentage is not null);

        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 1_000);
    }
}
