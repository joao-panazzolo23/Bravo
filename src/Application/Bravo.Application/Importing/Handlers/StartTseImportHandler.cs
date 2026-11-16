using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class StartTseImportHandler(ImportJobScheduler scheduler)
    : ICommandHandler<StartTseImportCommand, Result<ImportJobCreatedViewModel>>
{
    public ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        StartTseImportCommand command,
        CancellationToken cancellationToken)
        => new(scheduler.ScheduleAsync(
            TseImport.KeysFor(command.Year, command.State, command.Round, command.ElectionCode),
            command.ToRequest(),
            cancellationToken));
}
