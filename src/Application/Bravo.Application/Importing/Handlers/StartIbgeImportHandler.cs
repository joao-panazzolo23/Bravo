using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class StartIbgeImportHandler(ImportJobScheduler scheduler)
    : ICommandHandler<StartIbgeImportCommand, Result<ImportJobCreatedViewModel>>
{
    public ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartIbgeImportCommand command,
        CancellationToken cancellationToken)
        => new(scheduler.ScheduleAsync(
            IbgeImport.UnitKeys,
            command.ToRequest(),
            cancellationToken));
}
