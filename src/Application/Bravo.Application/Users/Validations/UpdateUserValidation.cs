using Bravo.Application.Extensions;
using Bravo.Application.Users.Commands;
using Bravo.Domain.Auth.Services;
using FluentValidation;

namespace Bravo.Application.Users.Validations;

internal class UpdateUserValidation : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidation(IPasswordQualityValidator passwordValidator)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Password)
            .MustHavePasswordQuality(passwordValidator);

        RuleFor(x => x.Email)
           .NotEmpty()
           .EmailAddress();

        RuleFor(x => x.Phone)
           .NotEmpty();

        RuleFor(x => x.Role)
          .IsInEnum();

        RuleFor(x => x.Document)
         .NotEmpty();
    }
}
