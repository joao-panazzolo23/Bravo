using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

public sealed record RetryImportJobCommand(
    Guid JobId
) : ICommand<Result<ImportJobCreatedViewModel>>;
