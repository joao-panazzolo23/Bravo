using Bravo.Application.Importing.Commands;
using FluentValidation;

namespace Bravo.Application.Importing.Validators;

public sealed class CreateImportJobCommandValidator : AbstractValidator<CreateImportJobCommand>
{
    public CreateImportJobCommandValidator()
    {
        RuleFor(command => command.ImportType)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.UnitKeys)
            .NotEmpty()
            .Must(keys => keys.All(key => !string.IsNullOrWhiteSpace(key)))
            .WithMessage("Import work unit keys must not be empty.")
            .Must(keys => keys.Select(key => key.Trim()).Distinct(StringComparer.Ordinal).Count() == keys.Count)
            .WithMessage("Import work unit keys must be unique.");

        RuleForEach(command => command.UnitKeys)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.BatchSize)
            .InclusiveBetween(1, 100_000)
            .When(command => command.BatchSize.HasValue);

        RuleFor(command => command.MaxAttempts)
            .InclusiveBetween(1, 1_000)
            .When(command => command.MaxAttempts.HasValue);

        RuleFor(command => command.LeaseDurationSeconds)
            .InclusiveBetween(1, 86_400)
            .When(command => command.LeaseDurationSeconds.HasValue);

        RuleFor(command => command.ConflictStrategy)
            .IsInEnum();
    }
}
