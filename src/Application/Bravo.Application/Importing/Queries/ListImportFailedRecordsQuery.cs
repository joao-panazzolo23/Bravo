using Bravo.Application.Importing.ViewModels;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Queries;

public sealed record ListImportFailedRecordsQuery(
    Guid JobId,
    int Limit = 100
) : IQuery<Result<IReadOnlyList<ImportFailedRecordViewModel>>>;
