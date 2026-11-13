using Bravo.Presentation.Conventions.Abstract;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions.Pipelines;

public class OpenApiConventionPipeline(IEnumerable<OpenApiConventionBase> conventions)
{
    public async Task Run(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken ct)
    {
        foreach (var convention in conventions)
            await convention.ApplyAsync(operation, context, ct);
    }
}
