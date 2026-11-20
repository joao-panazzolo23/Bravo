using Bravo.Domain.Auth.Services;
using FluentValidation;

namespace Bravo.Application.Extensions;

public static class ValidationHelpers
{
    public static IRuleBuilderOptionsConditions<T, string?> MustHavePasswordQuality<T>(
        this IRuleBuilder<T, string?> rule, IPasswordQualityValidator validator)
        => rule.Custom((password, context) =>
        {
            var result = validator.Validate(password ?? string.Empty);

            if (result.IsValid)
                return;

            foreach (var error in result.Errors)
                context.AddFailure(error);
        });
}
