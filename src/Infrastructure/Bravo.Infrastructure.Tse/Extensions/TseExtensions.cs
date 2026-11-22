using Bravo.Application.Imports.Common.Options;
using Bravo.Domain.Electoral.Services;
using Bravo.Infrastructure.Tse.Acquisition;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Bravo.Infrastructure.Tse.Reference;
using Bravo.Infrastructure.Tse.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Extensions;

/// <summary>
/// Registers the concrete TSE acquisition infrastructure.
/// </summary>
/// <remarks>
/// This project owns HTTP, archive extraction, CSV parsing and the typed readers. It deliberately
/// registers no application orchestrator: the import facade and its stage importers belong to
/// <c>Bravo.Application.Imports.Tse</c> and are registered by the Application composition root.
///
/// The readers are singletons because they are stateless adapters over a stream; the acquirer is
/// scoped because it uses a scoped <see cref="HttpClient"/> from the typed-client factory.
/// </remarks>
public static class TseExtensions
{
    public static IServiceCollection AddTse(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddTseOptions(configuration)
                       .AddTseHttpClient()
                       .AddTseAcquisition();
    }

    public static IServiceCollection AddTseOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TseOptions>()
            .Bind(configuration.GetSection(TseOptions.SectionName))
            .ValidateOnStart();

        // The resource limits are process-wide, so they are read here where the acquisition
        // infrastructure lives. The governor itself is registered by the Application execution
        // module, which owns that shared concept.
        services.AddOptions<ImportResourceOptions>()
            .Bind(configuration.GetSection(ImportResourceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

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

    public static IServiceCollection AddTseAcquisition(this IServiceCollection services)
    {
        return services.AddSingleton<TseArchiveExtractor>()
                       .AddSingleton<TseBackoffPolicy>()
                       .AddScoped<ITseDatasetAcquirer, TseDatasetAcquirer>()
                       .AddSingleton<ITseReferenceReader, TseReferenceReader>()
                       .AddSingleton<ITseCandidateReader, TseCandidateReader>()
                       .AddSingleton<ITseSectionResultReader, TseSectionResultReader>();
    }
}
