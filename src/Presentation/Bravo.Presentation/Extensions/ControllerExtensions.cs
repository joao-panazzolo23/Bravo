using System.Text.Json.Serialization;

namespace Bravo.Presentation.Extensions;

public static class ControllerExtensions
{
    public static IServiceCollection AddControllerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer()
                .AddDistributedMemoryCache()
                .AddControllers(options =>
                {
                    options.ReturnHttpNotAcceptable = true;
                })
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }
}
