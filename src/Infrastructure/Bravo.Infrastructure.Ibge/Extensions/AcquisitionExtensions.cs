using Bravo.Domain.Importing.Services;
using Bravo.Infrastructure.Acquisition;
using Bravo.Infrastructure.Acquisition.Importing;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.RateLimiting;
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

        return services.AddScoped<IImportSource, IbgeImportSource>();
    }
}
