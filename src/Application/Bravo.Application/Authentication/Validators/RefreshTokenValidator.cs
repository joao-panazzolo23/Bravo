using Bravo.Application.Authentication.Commands;
using FluentValidation;

namespace Bravo.Application.Authentication.Validators;

internal class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
