using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

public sealed record StartIbgeImportCommand(
    int? BatchSize = null,
    int? MaxAttempts = null,
    int? MaxParallelism = null,
    int? LeaseDurationSeconds = null)
    : ICommand<Result<ImportJobCreatedViewModel>>;
