using Bravo.Application.Acquisition.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Acquisition.Queries;

public record GetImportStatusQuery(Guid ExecutionId) : IQuery<Result<ImportStatusViewModel?>>;

