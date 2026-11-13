namespace Bravo.Presentation.Extensions;

public static class ControllerExtensions
{
    public static IServiceCollection AddControllerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer()
                .AddDistributedMemoryCache()
                .AddControllers(O =>
                {
                    //when client sends an unsupported format type, returns 406 -> we just accept json
                    O.ReturnHttpNotAcceptable = true;
                });

        return services;
    }
}
