using Bravo.Infrastructure.Tse.Downloads;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Extensions;

public static class TseExtensions
{
    public static IServiceCollection AddTse(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TseOptions>()
            .Bind(configuration.GetSection(TseOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(provider => new TseRateLimiter(
            provider.GetRequiredService<IOptions<TseOptions>>().Value));

        services.AddSingleton<TseBackoffPolicy>();

        services.AddHttpClient(TseHttpClient.Name, (provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<TseOptions>>().Value;

            client.BaseAddress = new Uri(settings.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        return services.AddSingleton<TseFileDownloader>()
                       .AddSingleton<TseArchiveExtractor>();
    }
}
