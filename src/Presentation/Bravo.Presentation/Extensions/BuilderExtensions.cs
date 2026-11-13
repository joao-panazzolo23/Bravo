namespace Bravo.Presentation.Extensions;

public static class BuilderExtensions
{
    public static IApplicationBuilder UseDocumentation(this IApplicationBuilder builder)
    {
        if (builder is WebApplication webApp && webApp.Environment.IsDevelopment())
            webApp.UseScalarUI();

        return builder;
    }
    public static IApplicationBuilder UseApplication(this IApplicationBuilder builder)
    {
        ((WebApplication)builder).UseStaticAssets().MapControllers();
        return builder;
    }

    public static IApplicationBuilder UseSecurity(this IApplicationBuilder services)
    {
        return services.UseCors(SecurityExtensions.CorsPolicy)
                       .UseAuthentication()
                       .UseAuthorization()
                       .UseRateLimiter();
    }
}
