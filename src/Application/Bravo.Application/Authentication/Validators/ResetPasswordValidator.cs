using Bravo.Application.Authentication.Commands;
using Bravo.Application.Extensions;
using Bravo.Domain.Auth.Services;
using FluentValidation;

namespace Bravo.Application.Authentication.Validators;

internal class ResetPasswordValidation : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidation(IPasswordQualityValidator passwordValidator)
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.NewPassword)
            .MustHavePasswordQuality(passwordValidator);
    }
}
