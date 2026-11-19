using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Domain.Importing.Enums;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.QueryHandlers;

/// <summary>
/// Builds a progress view from data already persisted on the job and its work units.
/// </summary>
/// <remarks>
/// No extra aggregate table exists: work-unit counts are derived from unit statuses, and record
/// counters are the sum of what each unit last reported. This keeps polling cheap and avoids a
/// second source of truth that could drift.
/// </remarks>
public sealed class GetImportJobProgressHandler(
    IImportJobRepository jobs,
    IImportWorkUnitRepository workUnits)
    : IQueryHandler<GetImportJobProgressQuery, Result<ImportJobProgressViewModel?>>
{
    public async ValueTask<Result<ImportJobProgressViewModel?>> Handle(
        GetImportJobProgressQuery query,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(query.JobId, cancellationToken);

        if (job is null)
            return Result<ImportJobProgressViewModel?>.NotFound(message: "Import job not found.");

        var units = await workUnits.GetByJobAsync(query.JobId, cancellationToken);

        var completed = units.Count(unit => unit.Status == ImportWorkUnitStatus.Completed);
        var failed = units.Count(unit => unit.Status == ImportWorkUnitStatus.Failed);
        var pending = units.Count(unit => unit.Status == ImportWorkUnitStatus.Pending);
        var running = units.Count(unit => unit.Status == ImportWorkUnitStatus.Running);
        var cancelled = units.Count(unit => unit.Status == ImportWorkUnitStatus.Cancelled);
        var retried = units.Count(unit => unit.AttemptCount > 0);

        var total = units.Count;
        var percentage = total == 0 ? 0d : Math.Round((double)completed / total * 100, 2);

        var current = units.FirstOrDefault(unit => unit.Status == ImportWorkUnitStatus.Running);

        var processed = units.Sum(unit => unit.ProcessedRecords);
        var inserted = units.Sum(unit => unit.InsertedRecords);
        var updated = units.Sum(unit => unit.UpdatedRecords);
        var rejected = units.Sum(unit => unit.RejectedRecords);
        var recordFailed = units.Sum(unit => unit.FailedRecords);

        // A record total is only reported when every unfinished unit also declares one;
        // otherwise a percentage would be misleading.
        var declaredTotals = units
            .Where(unit => unit.TotalRecords is not null)
            .Sum(unit => unit.TotalRecords!.Value);

        var hasCompleteTotal = units.Count > 0 && units.All(unit => unit.TotalRecords is not null);

        var recordTotal = hasCompleteTotal ? declaredTotals : (long?)null;

        var recordPercentage = recordTotal is > 0
            ? Math.Round((double)processed / recordTotal.Value * 100, 2)
            : (double?)null;

        var elapsed = job.StartedAt is { } startedAt
            ? (job.CompletedAt ?? DateTime.UtcNow) - startedAt
            : TimeSpan.Zero;

        var lastProgressAt = units
            .Where(unit => unit.ProgressUpdatedAt is not null)
            .Max(unit => unit.ProgressUpdatedAt);

        var recordsPerSecond = elapsed.TotalSeconds > 1 && processed > 0
            ? Math.Round(processed / elapsed.TotalSeconds, 2)
            : (double?)null;

        var unitsPerMinute = elapsed.TotalMinutes > 0.05
            ? Math.Round(completed / elapsed.TotalMinutes, 2)
            : (double?)null;

        var estimatedRemaining = EstimateRemainingSeconds(job, total, completed, elapsed);

        var viewModel = new ImportJobProgressViewModel(
            job.Id,
            job.Status.ToString(),
            percentage,
            current?.Stage.ToString(),
            current?.UnitKey,
            Math.Round(elapsed.TotalSeconds, 1),
            estimatedRemaining,
            lastProgressAt,
            new ImportProgressWorkUnits(
                total, completed, failed, pending, running, retried, percentage),
            new ImportProgressRecords(
                recordTotal, processed, inserted, updated, rejected, recordFailed, recordPercentage),
            new ImportProgressThroughput(recordsPerSecond, unitsPerMinute));

        return Result<ImportJobProgressViewModel?>.OK(viewModel);
    }

    /// <summary>
    /// Estimates remaining time from work-unit throughput only.
    /// </summary>
    /// <remarks>
    /// Deliberately avoids a record-based ETA: units differ greatly in size, so extrapolating
    /// from processed records would produce a misleading estimate.
    /// </remarks>
    private static double? EstimateRemainingSeconds(
        Domain.Importing.Entities.ImportJob job,
        int total,
        int completed,
        TimeSpan elapsed)
    {
        if (job.IsTerminal || completed == 0 || total == 0 || elapsed.TotalSeconds <= 1)
            return null;

        var remaining = total - completed;

        if (remaining <= 0)
            return 0;

        var perUnit = elapsed.TotalSeconds / completed;

        return Math.Round(perUnit * remaining, 0);
    }
}
