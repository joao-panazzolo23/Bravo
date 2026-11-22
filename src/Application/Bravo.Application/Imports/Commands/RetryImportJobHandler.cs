using Bravo.Application.Imports.Common.Commands;
using Bravo.Application.Imports.Ibge.Commands;
using Bravo.Application.Imports.Tse.Commands;
using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Repositories;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Commands;

/// <summary>
/// Retries a failed job by re-executing the whole source. Every stage is upsert-based, so a
/// second run is safe by construction; no per-unit bookkeeping needs to be reconstructed.
/// </summary>
public sealed class RetryImportJobHandler(IImportJobRepository jobs)
    : ICommandHandler<RetryImportJobCommand, Result<ImportJobCreatedViewModel>>
{
    public async ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        RetryImportJobCommand command,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(command.JobId, cancellationToken);

        if (job is null)
            return Result<ImportJobCreatedViewModel>.NotFound(message: "Import job not found.");

        if (job.Status != ImportJobStatus.Failed)
        {
            return Result<ImportJobCreatedViewModel>.BadRequest(
                message: "Only failed import jobs can be retried.");
        }

        if (!await jobs.RequeueAsync(command.JobId, cancellationToken))
        {
            return Result<ImportJobCreatedViewModel>.Conflict(
                message: $"An import job for '{job.Source}' and scope '{job.Scope}' is already pending or running.");
        }

        return Result<ImportJobCreatedViewModel>.Created(
            new ImportJobCreatedViewModel(job.Id, ImportJobStatus.Pending.ToString(), []));
    }
}
