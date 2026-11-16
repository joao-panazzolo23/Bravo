using Bravo.Application.Importing.Commands;
using FluentValidation;

namespace Bravo.Application.Importing.Validators;

public sealed class StartTseImportCommandValidator : AbstractValidator<StartTseImportCommand>
{
    public StartTseImportCommandValidator()
    {
        RuleFor(command => command.Year)
            .InclusiveBetween(1994, 2100);

        RuleFor(command => command.State)
            .NotEmpty()
            .Length(2)
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("State must be a two-letter acronym, or 'BR' for nationwide data.");

        RuleFor(command => command.Round)
            .InclusiveBetween((short)1, (short)2);

        RuleFor(command => command.ElectionCode)
            .GreaterThan(0);

        RuleFor(command => command.BatchSize)
            .InclusiveBetween(1, 100_000)
            .When(command => command.BatchSize.HasValue);

        RuleFor(command => command.MaxAttempts)
            .InclusiveBetween(1, 1_000)
            .When(command => command.MaxAttempts.HasValue);

        RuleFor(command => command.LeaseDurationSeconds)
            .InclusiveBetween(1, 86_400)
            .When(command => command.LeaseDurationSeconds.HasValue);
    }
}
