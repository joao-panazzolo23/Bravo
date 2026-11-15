using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Commands;

public sealed record RetryImportJobCommand(
    Guid JobId
) : ICommand<Result<ImportJobCreatedViewModel>>;
