using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Services;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class ExecuteImportJobHandler(ImportJobExecutor executor)
    : ICommandHandler<ExecuteImportJobCommand, Result<ImportJobExecutionResult>>
{
    public async ValueTask<Result<ImportJobExecutionResult>> Handle(
        ExecuteImportJobCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await executor.ExecuteAsync(
            command.JobId,
            command.WorkerId,
            cancellationToken);

        return Result<ImportJobExecutionResult>.OK(result);
    }
}
