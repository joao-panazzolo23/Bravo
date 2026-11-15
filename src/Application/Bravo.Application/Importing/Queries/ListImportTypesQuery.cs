using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.Queries;

public sealed record ListImportTypesQuery
    : IQuery<Result<IReadOnlyList<string>>>;
