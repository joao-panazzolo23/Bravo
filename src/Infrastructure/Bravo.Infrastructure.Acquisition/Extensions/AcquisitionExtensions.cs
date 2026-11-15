using Bravo.Infrastructure.Acquisition.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.Acquisition.Extensions;

public static class AcquisitionExtensions
{
    public static IServiceCollection AddAcquisition(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IbgeOptions>()
            .Bind(configuration.GetSection("Acquisition:Ibge"))
            .ValidateOnStart();

        return services;
    }
}
