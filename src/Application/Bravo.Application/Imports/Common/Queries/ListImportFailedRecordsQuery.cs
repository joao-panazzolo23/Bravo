using Bravo.Application.Imports.Common.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Imports.Common.Queries;

public sealed record ListImportFailedRecordsQuery(
    Guid JobId,
    int Limit = 100
) : IQuery<Result<IReadOnlyList<ImportFailedRecordViewModel>>>;
