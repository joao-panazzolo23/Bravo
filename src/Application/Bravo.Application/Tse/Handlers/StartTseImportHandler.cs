using Bravo.Application.Imports.Tse.Commands;
using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;
using Bravo.Application.Imports.Execution;

namespace Bravo.Application.Tse.Handlers;

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
