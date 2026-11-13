using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Transformers;

/// <summary>
/// OpenAPI NuGet Package operates via JSON configuration made available at http://localhost:{yourPort}/openapi/v1.json/
/// or you can download JSON as txt in Scalar UI, at "Download OpenAPI Document" button.
/// 
/// It cannot be exactly managed but can be transformed.
/// Scalar uses this Json to map endpoints, parameters, authorization, etc...
/// 
/// You can find more configuration options at 
/// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-9.0
/// </summary>
public class JwtSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// In this particular case, We're adding Bearer scheme for requests with Authorization schema.
    /// 
    /// It will not affect public endpoints, but will require bearer tokens for locked endpoints.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
        )
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        var bearerScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization"
        };

        document.Components.SecuritySchemes["bearerAuth"] = bearerScheme;

        document.Security ??= [];
        document.Security.Clear();

        var requirements = new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("bearerAuth"), [] }
        };

        document.Security.Add(requirements);

        return Task.CompletedTask;
    }
}