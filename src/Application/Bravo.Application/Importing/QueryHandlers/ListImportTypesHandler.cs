using Bravo.Application.Importing.Contracts;
using Bravo.Application.Importing.Queries;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Importing.QueryHandlers;

public sealed class ListImportTypesHandler(IEnumerable<IImportPipeline> pipelines)
    : IQueryHandler<ListImportTypesQuery, Result<IReadOnlyList<string>>>
{
    public ValueTask<Result<IReadOnlyList<string>>> Handle(
        ListImportTypesQuery query,
        CancellationToken cancellationToken)
    {
        var importTypes = pipelines
            .Select(pipeline => pipeline.ImportType)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return ValueTask.FromResult(Result<IReadOnlyList<string>>.OK(importTypes));
    }
}
