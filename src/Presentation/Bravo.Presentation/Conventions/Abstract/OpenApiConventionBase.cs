using Bravo.Presentation.Exceptions.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Conventions.Abstract;

/// <summary>
/// Choosing an abstract class instead of an interface let you provide common error responses 
/// using the same method across all conventions.
/// </summary>
public abstract class OpenApiConventionBase
{
    private string ErrorMediaType => "application/problem+json";
    public abstract Task ApplyAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken ct);

    protected Dictionary<string, OpenApiMediaType> GetErrorTypeContent(OpenApiOperationTransformerContext context)
    {
        var schema = new OpenApiSchemaReference(nameof(ErrorResult), context.Document);

        var mediatype = new OpenApiMediaType()
        {
            Schema = schema
        };

        var content = new Dictionary<string, OpenApiMediaType>
        {
            [ErrorMediaType] = mediatype
        };

        return content;
    }
}
