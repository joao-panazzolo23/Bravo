using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Ibge.Commands;
using Bravo.Application.Imports.Tse.Commands;
using Bravo.Domain.Importing.Repositories;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

public sealed class CancelImportJobHandler(IImportJobRepository jobs)
    : ICommandHandler<CancelImportJobCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(
        CancelImportJobCommand command,
        CancellationToken cancellationToken
    )
    {
        var job = await jobs.GetAsync(command.JobId, cancellationToken);

        if (job is null)
            return Result<Unit>.NotFound(message: "Import job not found.");

        if (job.IsTerminal)
            return Result<Unit>.BadRequest(message: "Import job cannot be cancelled in its current state.");

        if (!await jobs.CancelAsync(command.JobId, cancellationToken))
            return Result<Unit>.Conflict(message: "Import job changed state before cancellation was applied.");

        return Result<Unit>.OK(Unit.Value);
    }
}
