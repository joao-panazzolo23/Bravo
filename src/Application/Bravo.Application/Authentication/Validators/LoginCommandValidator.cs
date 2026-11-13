using Bravo.Application.Authentication.Commands;
using FluentValidation;

namespace Bravo.Application.Authentication.Validators;

internal class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
        .NotEmpty();
    }
}
