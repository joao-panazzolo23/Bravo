using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Tse.Commands;

public sealed record StartTseImportCommand(
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? MaxParallelism = null,
    int? LeaseDurationSeconds = null
) : ICommand<Result<ImportJobCreatedViewModel>>;
