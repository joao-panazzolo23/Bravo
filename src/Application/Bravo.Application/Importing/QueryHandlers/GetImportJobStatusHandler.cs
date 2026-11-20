using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Repositories;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.QueryHandlers;

public sealed class GetImportJobStatusHandler(IImportJobRepository jobs)
    : IQueryHandler<GetImportJobStatusQuery, Result<ImportJobStatusViewModel?>>
{
    public async ValueTask<Result<ImportJobStatusViewModel?>> Handle(
        GetImportJobStatusQuery query,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(query.JobId, cancellationToken);

        if (job is null)
            return Result<ImportJobStatusViewModel?>.NotFound(message: "Import job not found.");

        var viewModel = new ImportJobStatusViewModel(
            job.Id,
            job.Source,
            job.Scope,
            job.Status.ToString(),
            job.Stage.ToString(),
            job.BatchSize,
            job.MaxAttempts,
            job.MaxParallelism,
            job.LeaseDurationSeconds,
            job.AttemptCount,
            job.TotalOperations,
            job.CompletedOperations,
            job.FailedOperations,
            job.TotalProcessedRecords,
            job.TotalFailedRecords,
            job.CurrentOperation,
            job.LastError,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt
        );

        return Result<ImportJobStatusViewModel?>.OK(viewModel);
    }
}
