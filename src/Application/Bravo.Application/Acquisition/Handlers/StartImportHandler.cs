using Bravo.Application.Acquisition.Commands;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Scopes;
using Bravo.Application.Acquisition.Services;
using Bravo.Application.Acquisition.ViewModels;
using Bravo.Domain.Acquisition.Entities;
using Bravo.Shared.Data;
using Bravo.Shared.Exceptions;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Acquisition.Handlers;

public class StartImportHandler(
    IImportExecutionRepository executionRepository,
    IImportExecutionQueue executionQueue,
    IUnityOfWork unityOfWork,
    ILogger<StartImportHandler> logger
) : ICommandHandler<StartImportCommand, Result<StartImportResult>>
{
    public async ValueTask<Result<StartImportResult>> Handle(StartImportCommand command, CancellationToken cancellationToken)
    {
        var parsedScope = ImportScope.Parse(command.OperationType, command.Scope);
        if (parsedScope.Data is null || parsedScope.StatusCode >= 400)
        {
            return Result<StartImportResult>.BadRequest(message: parsedScope.Message);
        }

        var scope = parsedScope.Data.Canonical == "all" ? null : parsedScope.Data.Canonical;

        var existing = await executionRepository.GetRunningByType(command.OperationType, scope, cancellationToken);
        if (existing is not null)
        {
            return Result<StartImportResult>.Conflict(
                message: "An import with the same operation type and scope is already in progress.");
        }

        var execution = ImportExecution.Create(command.OperationType, scope);
        await executionRepository.Add(execution, cancellationToken);

        try
        {
            await unityOfWork.Commit();
        }
        catch (ConcurrencyException)
        {
            // The partial unique index on active (operation type, scope) is the authoritative
            // overlap guard: a race between two requests is rejected here, not only by the
            // advisory lookup above.
            logger.LogInformation(
                "Concurrent import start rejected for {OperationType} {Scope}.",
                command.OperationType,
                scope);

            return Result<StartImportResult>.Conflict(
                message: "An import with the same operation type and scope is already in progress.");
        }

        await executionQueue.Enqueue(execution.Id, cancellationToken);

        return Result<StartImportResult>.Created(new StartImportResult(
            execution.Id,
            execution.Status.ToString(),
            "Import execution enqueued."
        ));
    }
}
