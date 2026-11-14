using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Domain.Acquisition.Enums;
using Bravo.Shared.Data;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.Handlers;

public class CancelImportHandler(
    IImportExecutionRepository executionRepository,
    IUnityOfWork unityOfWork
) : ICommandHandler<CancelImportCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(CancelImportCommand command, CancellationToken cancellationToken)
    {
        var execution = await executionRepository.GetById(command.ExecutionId, cancellationToken);
        if (execution is null)
        {
            return Result<Unit>.NotFound(message: "Import execution not found.");
        }

        if (execution.Status is ImportStatus.Completed or ImportStatus.Failed or ImportStatus.Cancelled)
        {
            return Result<Unit>.BadRequest(message: "Import cannot be cancelled in its current state.");
        }

        execution.RequestCancellation();
        await executionRepository.Update(execution, cancellationToken);
        await unityOfWork.Commit();

        return Result<Unit>.OK(Unit.Value);
    }
}

