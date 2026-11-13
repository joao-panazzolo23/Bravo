using Bravo.Presentation.Conventions.Abstract;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions;

public class ServerErrorConvention : OpenApiConventionBase
{
    public override Task ApplyAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
       var content = GetErrorTypeContent(context);

        var response = new OpenApiResponse
        {
            Description = "Internal Server Error",
            Content = content
        };

        operation.Responses?.TryAdd(StatusCodes.Status500InternalServerError.ToString(), response);

        return Task.CompletedTask;
    }
}
