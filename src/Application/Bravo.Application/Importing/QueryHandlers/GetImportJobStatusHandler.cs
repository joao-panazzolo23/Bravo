using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.QueryHandlers;

public sealed class GetImportJobStatusHandler(
    IImportJobRepository jobs,
    IImportWorkUnitRepository workUnits)
    : IQueryHandler<GetImportJobStatusQuery, Result<ImportJobStatusViewModel?>>
{
    public async ValueTask<Result<ImportJobStatusViewModel?>> Handle(
        GetImportJobStatusQuery query,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(query.JobId, cancellationToken);

        if (job is null)
            return Result<ImportJobStatusViewModel?>.NotFound(message: "Import job not found.");

        var units = await workUnits.GetByJobAsync(query.JobId, cancellationToken);

        var viewModel = new ImportJobStatusViewModel(
            job.Id,
            job.ImportType,
            job.Status.ToString(),
            job.ConflictStrategy.ToString(),
            job.BatchSize,
            job.MaxAttempts,
            job.LeaseDurationSeconds,
            job.TotalProcessedRecords,
            job.TotalFailedRecords,
            job.LastError,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            units.Select(unit => new ImportWorkUnitStatusViewModel(
                unit.Id,
                unit.UnitKey,
                unit.Status.ToString(),
                unit.Cursor,
                unit.ProcessedRecords,
                unit.FailedRecords,
                unit.AttemptCount,
                unit.LastError,
                unit.LeaseOwner,
                unit.LeaseExpiresAt,
                unit.FencingToken,
                unit.StartedAt,
                unit.CompletedAt)).ToArray());

        return Result<ImportJobStatusViewModel?>.OK(viewModel);
    }
}
