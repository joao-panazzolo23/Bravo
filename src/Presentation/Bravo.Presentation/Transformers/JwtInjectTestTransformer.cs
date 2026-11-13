using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bravo.Presentation.Transformers;

/// <summary>
/// Todo: inject Bearer JWT for testability
/// </summary>
public class JwtInjectTestTransformer(IServiceProvider provider,
                                      IConfiguration configuration) : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Transformer for creating JWT Token when under development mode.
    /// 
    /// TODO: This token wouldn't be more easily statically configured at appsettings.json?
    /// </summary>
    /// <param name="document"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        //var userConfig = _configuration.GetSection("TestUser").Get<LoginCommand>();

        //if (userConfig is not null)
        //{
        //    //using var scope = _provider.CreateScope();
        //    //var service = scope.ServiceProvider.GetRequiredService<IRequestHandler<LoginCommand, Result<LoginViewModel>>>();

        //    var response = (await service.Handle(userConfig, cancellationToken)).Data;

        //    if (response is not null)
        //    {
        //        document.Components ??= new OpenApiComponents();

        //        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        //        {
        //            {
        //                new OpenApiSecurityScheme
        //                {
        //                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearerAuth" }
        //                },
        //                new [] { response.Token }
        //            }
        //        });
        //    }

        //}
    }
}
