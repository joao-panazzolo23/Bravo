using Bravo.Application.Acquisition.Commands;
using FluentValidation;

namespace Bravo.Application.Acquisition.Validators;

public class StartImportCommandValidator : AbstractValidator<StartImportCommand>
{
    public StartImportCommandValidator()
    {
        RuleFor(x => x.OperationType)
            .IsInEnum();
    }
}

