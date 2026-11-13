using Bravo.Application.Extensions;
using Bravo.Application.Users.Commands;
using Bravo.Domain.Auth.Services;
using FluentValidation;

namespace Bravo.Application.Users.Validations;

public class CreateUserValidation : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidation(IPasswordQualityValidator passwordValidator)
    {
        RuleFor(x => x.Name).NotEmpty();

        RuleFor(x => x.Password)
            .MustHavePasswordQuality(passwordValidator);


        RuleFor(x => x.Email)
           .NotEmpty()
           .EmailAddress()
           .When(x => x.Email != null);

        RuleFor(x => x.Phone)
           .NotEmpty();

        RuleFor(x => x.Role)
          .IsInEnum();

        RuleFor(x => x.Document)
         .NotEmpty();
    }
}
