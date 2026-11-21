using Bravo.Application.Electoral.Services;
using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.Services;
using Bravo.Application.Importing.Sources.Tse;
using Bravo.Domain.Electoral.Services;
using Bravo.Domain.Importing.Services;
using Bravo.Infrastructure.Tse.Acquisition;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.RateLimiting;
using Bravo.Infrastructure.Tse.Reference;
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
        return services.AddTseOptions(configuration)
                       .AddTseRateLimiter()
                       .AddTseHttpClient()
                       .AddTseServices();
    }

    public static IServiceCollection AddTseOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TseOptions>()
            .Bind(configuration.GetSection(TseOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<ImportResourceOptions>()
            .Bind(configuration.GetSection(ImportResourceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddTseRateLimiter(this IServiceCollection services)
    {
        services.AddSingleton(provider => new TseRateLimiter(
            provider.GetRequiredService<IOptions<TseOptions>>()));

        return services;
    }

    /// <summary>
    /// Registers <see cref="TseHttpClient"/> as a typed client. The factory owns handler and
    /// connection lifetime; the client is transient and is only consumed by scoped services, so
    /// no instance is captured by a longer-lived consumer.
    /// </summary>
    public static IServiceCollection AddTseHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient<TseHttpClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<IOptions<TseOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
        });

        return services;
    }

    public static IServiceCollection AddTseServices(this IServiceCollection services)
    {
        // One governor per process: the limits it enforces are only meaningful if every scope
        // in every job shares the same instance.
        return services.AddScoped<IImportSource, TseImportSource>()
                       .AddScoped<DatasetVersionService>()
                       .AddScoped<TseMunicipalityResolver>()
                       .AddScoped<TseSyntheticScopeSeeder>()
                       .AddSingleton<TseArchiveExtractor>()
                       .AddScoped<ITseDatasetAcquirer, TseDatasetAcquirer>()
                       .AddSingleton<ITseReferenceReader, TseReferenceReader>()
                       .AddSingleton<ITseCandidateReader, TseCandidateReader>()
                       .AddSingleton<ITseSectionResultReader, TseSectionResultReader>()
                       .AddSingleton<TseBackoffPolicy>()
                       .AddSingleton<ImportResourceGovernor>();
    }
}
