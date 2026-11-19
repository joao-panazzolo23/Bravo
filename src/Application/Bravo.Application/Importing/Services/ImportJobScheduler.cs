using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Models;
using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Entities;
using Bravo.Shared.Result;
using Microsoft.Extensions.Options;
using Bravo.Domain.Importing.Repositories;
using Bravo.Application.Importing.Models;

namespace Bravo.Application.Importing.Services;

public sealed class ImportJobScheduler(
    IImportJobRepository jobs,
    IOptions<ImportEngineOptions> options)
{
    public async Task<Result<ImportJobCreatedViewModel>> ScheduleAsync(
        IReadOnlyList<string> unitKeys,
        ImportJobRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var job = ImportJob.Create(
            request.BatchSize ?? settings.DefaultBatchSize,
            request.MaxAttempts ?? settings.DefaultMaxAttempts,
            request.LeaseDurationSeconds ?? settings.DefaultLeaseDurationSeconds);

        var units = unitKeys
            .Select(key => ImportWorkUnit.Create(job.Id, key))
            .ToArray();

        if (!await jobs.CreateAsync(job, units, cancellationToken))
        {
            return Result<ImportJobCreatedViewModel>.Conflict(
                message: "An import job is already running for the requested scope.");
        }

        return Result<ImportJobCreatedViewModel>.Created(
            new ImportJobCreatedViewModel(job.Id, job.Status.ToString(), unitKeys));
    }
}
