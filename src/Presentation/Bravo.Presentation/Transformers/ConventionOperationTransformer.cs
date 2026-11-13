using Bravo.Presentation.Conventions;
using Bravo.Presentation.Conventions.Pipelines;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Transformers;

public class ConventionOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken ct)
    {
        //this is NOT going to be used many times, just on system startup.
        //I dont see an actual need to inject dependencies here.
        var pipeline = new OpenApiConventionPipeline([
                new AuthConvention(),
                new ValidationConvention(),
                new ServerErrorConvention(),
                new DeprecationConvention()
            ]);

        await pipeline.Run(operation, context, ct);
    }
}
