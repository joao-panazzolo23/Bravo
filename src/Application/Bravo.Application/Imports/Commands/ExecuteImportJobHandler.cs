using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Ibge.Commands;
using Bravo.Application.Imports.Tse.Commands;
using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

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
