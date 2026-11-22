using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Ibge.Commands;
using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Ibge.Handlers;

public sealed class StartIbgeImportHandler(ImportJobStarter starter)
    : ICommandHandler<StartIbgeImportCommand, Result<ImportJobCreatedViewModel>>
{
    public async ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartIbgeImportCommand command,
        CancellationToken cancellationToken)
    {
        return await starter.StartAsync(
                "IBGE",
                "IBGE:localities",
                command.BatchSize,
                command.MaxAttempts,
                command.MaxParallelism,
                command.LeaseDurationSeconds,
                cancellationToken);
    }

}
