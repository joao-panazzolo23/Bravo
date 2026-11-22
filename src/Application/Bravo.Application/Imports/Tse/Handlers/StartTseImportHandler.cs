using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Tse.Commands;
using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Application.Imports.Tse;
using Bravo.Application.Imports.Tse.Models;
using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Tse.Handlers;

public sealed class StartTseImportHandler(
    ImportJobStarter starter
) : ICommandHandler<StartTseImportCommand, Result<ImportJobCreatedViewModel>>
{
    public async ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartTseImportCommand command,
        CancellationToken cancellationToken
    )
    {
        return await starter.StartAsync(
            TseImportSource.SourceKey,
            TseImportSource.Scope,
            command.BatchSize,
            command.MaxAttempts,
            command.MaxParallelism,
            command.LeaseDurationSeconds,
            cancellationToken
        );
    }
}
