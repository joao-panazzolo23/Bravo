using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Queries;
using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;
using Bravo.Domain.Importing.Repositories;

namespace Bravo.Application.Importing.QueryHandlers;

public sealed class ListImportFailedRecordsHandler(
    IImportJobRepository jobs,
    IImportFailedRecordRepository failedRecords)
    : IQueryHandler<ListImportFailedRecordsQuery, Result<IReadOnlyList<ImportFailedRecordViewModel>>>
{
    public async ValueTask<Result<IReadOnlyList<ImportFailedRecordViewModel>>> Handle(
        ListImportFailedRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(query.JobId, cancellationToken);

        if (job is null)
            return Result<IReadOnlyList<ImportFailedRecordViewModel>>.NotFound(message: "Import job not found.");

        var records = await failedRecords.ListByJobAsync(query.JobId, query.Limit, cancellationToken);

        return Result<IReadOnlyList<ImportFailedRecordViewModel>>.OK(
            records.Select(record => new ImportFailedRecordViewModel(
                record.Id,
                record.WorkUnitId,
                record.SourceIdentifier,
                record.Payload,
                record.ErrorCategory.ToString(),
                record.ErrorDescription,
                record.AttemptCount,
                record.CreatedAt)).ToArray());
    }
}
