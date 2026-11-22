using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Queries;

public sealed record GetImportJobProgressQuery(Guid JobId)
    : IQuery<Result<ImportJobProgressViewModel?>>;
