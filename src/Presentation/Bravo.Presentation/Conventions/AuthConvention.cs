using Bravo.Presentation.Conventions.Abstract;
using Bravo.Presentation.Exceptions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions;

public class AuthConvention : OpenApiConventionBase
{
    public override async Task ApplyAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct);
        context.Document?.AddComponent(nameof(ErrorResult), schema);

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var hasAuthorize = metadata
            .OfType<IAuthorizeData>()
            .Any();

        var allowsAnonymous = metadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (!hasAuthorize || allowsAnonymous) return;

        operation.Responses?.TryAdd(StatusCodes.Status401Unauthorized.ToString(), new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses?.TryAdd(StatusCodes.Status403Forbidden.ToString(), new OpenApiResponse { Description = "Forbidden" });
    }
}



