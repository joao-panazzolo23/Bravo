using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Queries;

public sealed record GetImportJobStatusQuery(
    Guid JobId
) : IQuery<Result<ImportJobStatusViewModel?>>;
