using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.Sources.Tse;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

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
