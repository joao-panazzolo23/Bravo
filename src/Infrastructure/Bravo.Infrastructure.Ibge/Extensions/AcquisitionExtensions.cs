using Bravo.Application.Importing.Sources.Ibge;
using Bravo.Infrastructure.Ibge.Importing;
using Bravo.Infrastructure.Ibge.Options;
using Bravo.Infrastructure.Ibge.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.Ibge.Extensions;

public static class AcquisitionExtensions
{
    public static IServiceCollection AddAcquisition(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<IbgeOptions>()
            .Bind(configuration.GetSection("Acquisition:Ibge"))
            .ValidateOnStart();

        services.AddSingleton(provider => new IbgeRateLimiter(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IbgeOptions>>().Value));

        services.AddHttpClient(IbgeHttpClient.Name, (provider, client) =>
        {
            var settings = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<IbgeOptions>>().Value;

            client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        // Transport only. The IBGE import orchestration lives in the application layer
        // (IbgeImportSource) and is registered by AddApplication.
        return services.AddScoped<IIbgeReader, IbgeReader>();
    }
}
