using Bravo.Application.Users.Commands;
using FluentValidation;

namespace Bravo.Application.Users.Validations;

internal sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
