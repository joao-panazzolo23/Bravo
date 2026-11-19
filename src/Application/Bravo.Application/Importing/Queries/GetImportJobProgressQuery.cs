using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Queries;

public sealed record GetImportJobProgressQuery(Guid JobId)
    : IQuery<Result<ImportJobProgressViewModel?>>;
