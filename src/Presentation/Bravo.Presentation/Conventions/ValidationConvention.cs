using Bravo.Presentation.Conventions.Abstract;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions;

public class ValidationConvention : OpenApiConventionBase
{
    public override Task ApplyAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var hasBody = context.Description.ParameterDescriptions
            .Any(p => p.Source == BindingSource.Body);

        if (!hasBody) return Task.CompletedTask;

        var content = GetErrorTypeContent(context);

        var response = new OpenApiResponse
        {
            Description = "Bad Request — Validation failed",
            Content = content
        };

        operation.Responses?.TryAdd(StatusCodes.Status400BadRequest.ToString(), response);

        return Task.CompletedTask;
    }
}
