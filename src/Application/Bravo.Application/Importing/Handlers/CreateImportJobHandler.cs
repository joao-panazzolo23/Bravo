using Bravo.Application.Importing.Commands;
using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Entities;
using Bravo.Shared.Result;
using Mediator;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Importing.Handlers;

public sealed class CreateImportJobHandler(
    IImportJobRepository jobs,
    IEnumerable<IImportPipeline> pipelines,
    IOptions<ImportEngineOptions> options)
    : ICommandHandler<CreateImportJobCommand, Result<ImportJobCreatedViewModel>>
{
    public async ValueTask<Result<ImportJobCreatedViewModel>> Handle(
        CreateImportJobCommand command,
        CancellationToken cancellationToken)
    {
        if (!pipelines.Any(pipeline => pipeline.ImportType == command.ImportType))
        {
            return Result<ImportJobCreatedViewModel>.BadRequest(
                message: $"No import pipeline is registered for import type '{command.ImportType}'.");
        }

        var unitKeys = command.UnitKeys
            .Select(key => key.Trim())
            .ToArray();

        if (unitKeys.Any(string.IsNullOrEmpty))
        {
            return Result<ImportJobCreatedViewModel>.BadRequest(
                message: "Import work unit keys must not be empty.");
        }

        if (unitKeys.Distinct(StringComparer.Ordinal).Count() != unitKeys.Length)
        {
            return Result<ImportJobCreatedViewModel>.BadRequest(
                message: "Import work unit keys must be unique.");
        }

        var settings = options.Value;
        var job = ImportJob.Create(
            command.ImportType,
            command.ConflictStrategy,
            command.BatchSize ?? settings.DefaultBatchSize,
            command.MaxAttempts ?? settings.DefaultMaxAttempts,
            command.LeaseDurationSeconds ?? settings.DefaultLeaseDurationSeconds);

        var workUnits = unitKeys
            .Select(key => ImportWorkUnit.Create(job.Id, key))
            .ToArray();

        if (!await jobs.CreateAsync(job, workUnits, cancellationToken))
        {
            return Result<ImportJobCreatedViewModel>.Conflict(
                message: "An import job is already running for the requested scope.");
        }

        return Result<ImportJobCreatedViewModel>.Created(
            new ImportJobCreatedViewModel(job.Id, job.ImportType, job.Status.ToString(), unitKeys));
    }
}
