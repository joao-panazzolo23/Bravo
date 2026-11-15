using Bravo.Application.Acquisition.Commands;
using FluentValidation;

namespace Bravo.Application.Acquisition.Validators;

public class StartImportCommandValidator : AbstractValidator<StartImportCommand>
{
    public StartImportCommandValidator()
    {
        RuleFor(x => x.OperationType)
            .IsInEnum();

        RuleFor(x => x.Scope)
            .MaximumLength(200)
            .WithMessage("The import scope must not exceed 200 characters.");
    }
}
