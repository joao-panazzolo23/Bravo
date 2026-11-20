using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class StartIbgeImportHandler(ImportJobStarter starter)
    : ICommandHandler<StartIbgeImportCommand, Result<ImportJobCreatedViewModel>>
{
    public ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartIbgeImportCommand command,
        CancellationToken cancellationToken)
        => new(starter.StartAsync(
            "IBGE",
            "IBGE:localities",
            command.BatchSize,
            command.MaxAttempts,
            command.MaxParallelism,
            command.LeaseDurationSeconds,
            cancellationToken)
        );
}
