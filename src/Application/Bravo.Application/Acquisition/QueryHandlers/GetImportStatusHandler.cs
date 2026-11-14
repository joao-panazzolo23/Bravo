using Bravo.Application.Acquisition.Queries;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.QueryHandlers;

public class GetImportStatusHandler(
    IImportExecutionRepository executionRepository
) : IQueryHandler<GetImportStatusQuery, Result<ImportStatusViewModel?>>
{
    public async ValueTask<Result<ImportStatusViewModel?>> Handle(GetImportStatusQuery query, CancellationToken cancellationToken)
    {
        var execution = await executionRepository.GetById(query.ExecutionId, cancellationToken);
        if (execution is null)
        {
            return Result<ImportStatusViewModel?>.NotFound(message: "Import execution not found.");
        }

        var viewModel = new ImportStatusViewModel(
            execution.Id,
            execution.OperationType.ToString(),
            execution.Scope,
            execution.Status.ToString(),
            execution.CreatedAt,
            execution.StartedAt,
            execution.CompletedAt,
            execution.CurrentStage,
            execution.RecordsProcessed,
            execution.RecordsPersisted,
            execution.FailureDetails,
            execution.CancellationReason
        );

        return Result<ImportStatusViewModel?>.OK(viewModel);
    }
}

