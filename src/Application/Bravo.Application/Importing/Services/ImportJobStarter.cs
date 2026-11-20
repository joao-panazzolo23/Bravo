using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Repositories;
using Bravo.Shared.Result;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Persists a new import job and hides the overlap rule from the command handlers.
/// </summary>
public sealed class ImportJobStarter(
    IImportJobRepository jobs,
    IOptions<ImportEngineOptions> options
)
{
    public async Task<Result<ImportJobCreatedViewModel>> StartAsync(
        string source,
        string scope,
        int? batchSize,
        int? maxAttempts,
        int? maxParallelism,
        int? leaseDurationSeconds,
        CancellationToken cancellationToken
    )
    {
        var settings = options.Value;

        var job = ImportJob.Create(
            source,
            scope,
            batchSize ?? settings.DefaultBatchSize,
            maxAttempts ?? settings.DefaultMaxAttempts,
            maxParallelism ?? settings.DefaultMaxParallelism,
            leaseDurationSeconds ?? settings.DefaultLeaseDurationSeconds
        );

        if (!await jobs.CreateAsync(job, cancellationToken))
        {
            return Result<ImportJobCreatedViewModel>.Conflict(
                message: $"An import job for '{source}' and scope '{scope}' is already pending or running.");
        }

        var viewmodel = new ImportJobCreatedViewModel(job.Id, job.Status.ToString(), []);

        return Result<ImportJobCreatedViewModel>.Created(
            viewmodel
            );
    }
}
