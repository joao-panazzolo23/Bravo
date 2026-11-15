using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Handlers;

public sealed class RetryImportJobHandler(
    IImportJobRepository jobs,
    IImportWorkUnitRepository workUnits,
    IEnumerable<IImportPipeline> pipelines)
    : ICommandHandler<RetryImportJobCommand, Result<ImportJobCreatedViewModel>>
{
    public async ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        RetryImportJobCommand command,
        CancellationToken cancellationToken)
    {
        var original = await jobs.GetAsync(command.JobId, cancellationToken);

        if (original is null)
            return Result<ImportJobCreatedViewModel>.NotFound(message: "Import job not found.");

        if (original.Status != ImportJobStatus.Failed)
            return Result<ImportJobCreatedViewModel>.BadRequest(message: "Only failed import jobs can be retried.");

        if (!pipelines.Any(pipeline => pipeline.ImportType == original.ImportType))
        {
            return Result<ImportJobCreatedViewModel>.BadRequest(
                message: $"No import pipeline is registered for import type '{original.ImportType}'.");
        }

        var failedKeys = await workUnits.GetFailedKeysAsync(command.JobId, cancellationToken);

        if (failedKeys.Count == 0)
            return Result<ImportJobCreatedViewModel>.BadRequest(message: "Import job has no failed work units.");

        var job = ImportJob.Create(
            original.ImportType,
            original.ConflictStrategy,
            original.BatchSize,
            original.MaxAttempts,
            original.LeaseDurationSeconds);

        var units = failedKeys
            .Select(key => ImportWorkUnit.Create(job.Id, key))
            .ToArray();

        if (!await jobs.CreateAsync(job, units, cancellationToken))
        {
            return Result<ImportJobCreatedViewModel>.Conflict(
                message: "An import job is already running for the requested scope.");
        }

        return Result<ImportJobCreatedViewModel>.Created(
            new ImportJobCreatedViewModel(job.Id, job.ImportType, job.Status.ToString(), failedKeys));
    }
}
