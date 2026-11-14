using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Application.Acquisition.ViewModels;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.Handlers;

public class StartImportHandler(
    IImportExecutionRepository executionRepository,
    IImportExecutionQueue executionQueue,
    IUnityOfWork unityOfWork
) : ICommandHandler<StartImportCommand, Result<StartImportResult>>
{
    public async ValueTask<Result<StartImportResult>> Handle(StartImportCommand command, CancellationToken cancellationToken)
    {
        var existing = await executionRepository.GetRunningByType(command.OperationType, command.Scope, cancellationToken);
        if (existing is not null)
        {
            return Result<StartImportResult>.Conflict(message: "An import with the same operation type and scope is already in progress.");
        }

        var execution = ImportExecution.Create(command.OperationType, command.Scope);
        await executionRepository.Add(execution, cancellationToken);
        await unityOfWork.Commit();

        await executionQueue.Enqueue(execution.Id, cancellationToken);

        return Result<StartImportResult>.Created(new StartImportResult(
            execution.Id,
            execution.Status.ToString(),
            "Import execution enqueued."
        ));
    }
}

