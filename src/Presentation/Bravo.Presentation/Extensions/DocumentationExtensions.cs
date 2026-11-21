using Bravo.Presentation.Transformers;
using Scalar.AspNetCore;

namespace Bravo.Presentation.Extensions;

public static class DocumentationExtensions
{
    /// <summary>
    /// Enables OpenAPI/ScalarUI
    /// </summary>
    /// <param name="services"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    public static IServiceCollection AddDocumentation(
        this IServiceCollection services,
        IHostEnvironment environment
        )
    {
        if (!environment.IsDevelopment()) return services;

        return services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<JwtSecuritySchemeTransformer>();
            options.AddDocumentTransformer<JwtInjectTestTransformer>();
            options.AddOperationTransformer<ConventionOperationTransformer>();
        });
    }

    public static WebApplication UseScalarUI(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;

        app.MapOpenApi("/openapi/{documentName}.json")
           .AllowAnonymous();

        app.MapScalarApiReference(opt =>
        {
            opt.WithClassicLayout()
               .WithTheme(ScalarTheme.DeepSpace)
               .SortTagsAlphabetically()
               .SortOperationsByMethod()
               .AddPreferredSecuritySchemes("bearerAuth")
               .WithTitle("Bravo API V1.0")
               .WithOpenApiRoutePattern("/openapi/{documentName}.json")
               .DisableTelemetry();
        })
           .AllowAnonymous();

        return app;
    }

}
