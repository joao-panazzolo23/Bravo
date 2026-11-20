using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Repositories;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.QueryHandlers;

public sealed class GetImportJobProgressHandler(IImportJobRepository jobs)
    : IQueryHandler<GetImportJobProgressQuery, Result<ImportJobProgressViewModel?>>
{
    public async ValueTask<Result<ImportJobProgressViewModel?>> Handle(
        GetImportJobProgressQuery query,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(query.JobId, cancellationToken);

        if (job is null)
            return Result<ImportJobProgressViewModel?>.NotFound(message: "Import job not found.");

        //todo: i dont like ternary ops
        var percentage = job.TotalOperations == 0
            ? 0d
            : Math.Round((double)job.CompletedOperations / job.TotalOperations * 100, 2);

        var elapsed = job.StartedAt is { } startedAt
            ? (job.CompletedAt ?? DateTime.UtcNow) - startedAt
            : TimeSpan.Zero;

        var recordsPerSecond = elapsed.TotalSeconds > 1 && job.TotalProcessedRecords > 0
            ? Math.Round(job.TotalProcessedRecords / elapsed.TotalSeconds, 2)
            : (double?)null;

        var operationsPerMinute = elapsed.TotalMinutes > 0.05
            ? Math.Round(job.CompletedOperations / elapsed.TotalMinutes, 2)
            : (double?)null;

        var units = new ImportProgressWorkUnits(
            job.TotalOperations,
            job.CompletedOperations,
            job.FailedOperations,
            Math.Max(0, job.TotalOperations - job.CompletedOperations - job.FailedOperations),
            job.CurrentOperation is null ? 0 : 1,
            job.AttemptCount > 1 ? 1 : 0,
            percentage);

        var records = new ImportProgressRecords(
            null,
            job.TotalProcessedRecords,
            job.TotalProcessedRecords,
            0,
            0,
            job.TotalFailedRecords,
            null
        );

        var throughput = new ImportProgressThroughput(recordsPerSecond, operationsPerMinute);

        var viewModel = new ImportJobProgressViewModel(
            job.Id,
            job.Status.ToString(),
            percentage,
            job.Stage.ToString(),
            job.CurrentOperation,
            Math.Round(elapsed.TotalSeconds, 1),
            EstimateRemainingSeconds(job.IsTerminal, job.TotalOperations, job.CompletedOperations, elapsed),
            job.ProgressUpdatedAt,
            units,
            records,
            throughput
           );

        return Result<ImportJobProgressViewModel?>.OK(viewModel);
    }

    /// <summary>
    /// Estimates remaining time from completed operations. A record-based estimate is
    /// deliberately avoided because operations differ greatly in size.
    /// </summary>
    private static double? EstimateRemainingSeconds(
        bool isTerminal,
        int total,
        int completed,
        TimeSpan elapsed)
    {
        if (isTerminal || completed == 0 || total == 0 || elapsed.TotalSeconds <= 1)
            return null;

        var remaining = total - completed;

        if (remaining <= 0)
            return 0;

        return Math.Round(elapsed.TotalSeconds / completed * remaining, 0);
    }
}
