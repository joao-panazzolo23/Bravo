using Bravo.Presentation.Conventions.Abstract;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions;

public class DeprecationConvention : OpenApiConventionBase
{
    public override Task ApplyAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        operation.Deprecated = metadata
            .OfType<ObsoleteAttribute>()
            .Any();

        return Task.CompletedTask;
    }
}
