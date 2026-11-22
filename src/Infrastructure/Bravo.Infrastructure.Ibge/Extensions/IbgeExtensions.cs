using Bravo.Application.Imports.Ibge;
using Bravo.Application.Imports.Ibge.Abstractions;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Ibge.Importing;
using Bravo.Infrastructure.Ibge.Options;
using Bravo.Infrastructure.Ibge.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Ibge.Extensions;

public static class IbgeExtensions
{
    public static IServiceCollection AddIbge(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddIbgeOptions(configuration)
                       .AddIbgeRateLimiter()
                       .AddIbgeHttpClient()
                       .AddIbgeReader();
    }

    public static IServiceCollection AddIbgeOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<IbgeOptions>()
            .Bind(configuration.GetSection(IbgeOptions.SectionName))
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddIbgeRateLimiter(this IServiceCollection services)
    {
        services.AddSingleton(provider => new IbgeRateLimiter(
            provider.GetRequiredService<IOptions<IbgeOptions>>().Value));

        return services;
    }

    /// <summary>
    /// Registers <see cref="IbgeHttpClient"/> as a typed client. The factory owns the handler
    /// lifetime; the rate limiter is injected separately so the limit is shared by every
    /// caller rather than recreated per client instance.
    /// </summary>
    public static IServiceCollection AddIbgeHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient<IbgeHttpClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<IOptions<IbgeOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
        });

        return services;
    }

    public static IServiceCollection AddIbgeReader(this IServiceCollection services)
    {
        services.AddScoped<IIbgeReader, IbgeReader>();

        return services;
    }
}
