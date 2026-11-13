using Bravo.Application.Authentication.Commands;
using FluentValidation;

namespace Bravo.Application.Authentication.Validators;

internal class RecoveryPasswordValidation : AbstractValidator<RecoveryPasswordCommand>
{
    public RecoveryPasswordValidation()
    {
        RuleFor(x => x.Email)
           .NotEmpty()
           .EmailAddress()
           .MaximumLength(256);
    }
}
