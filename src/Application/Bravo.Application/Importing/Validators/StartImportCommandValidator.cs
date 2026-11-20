using Bravo.Application.Importing.Commands;
using FluentValidation;

namespace Bravo.Application.Importing.Validators;

public sealed class StartImportCommandValidator : AbstractValidator<StartTseImportCommand>
{
    public StartImportCommandValidator()
    {
        RuleFor(command => command.BatchSize)
            .InclusiveBetween(1, 100_000)
            .When(command => command.BatchSize.HasValue);

        RuleFor(command => command.MaxAttempts)
            .InclusiveBetween(1, 1_000)
            .When(command => command.MaxAttempts.HasValue);

        RuleFor(command => command.MaxParallelism)
            .InclusiveBetween(1, 64)
            .When(command => command.MaxParallelism.HasValue);

        RuleFor(command => command.LeaseDurationSeconds)
            .InclusiveBetween(1, 86_400)
            .When(command => command.LeaseDurationSeconds.HasValue);
    }
}
