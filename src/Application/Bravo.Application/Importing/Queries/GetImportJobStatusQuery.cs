using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Queries;

public sealed record GetImportJobStatusQuery(Guid JobId)
    : IQuery<Result<ImportJobStatusViewModel?>>;
