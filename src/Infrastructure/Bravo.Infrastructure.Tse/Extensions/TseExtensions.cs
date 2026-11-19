using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Electoral.Services;
using Bravo.Application.Importing.Contracts;
using Bravo.Infrastructure.Tse.Acquisition;
using Bravo.Infrastructure.Tse.Downloads;
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
        services.AddOptions<TseOptions>()
            .Bind(configuration.GetSection(TseOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(provider => new TseRateLimiter(
            provider.GetRequiredService<IOptions<TseOptions>>()));

        services.AddSingleton<TseBackoffPolicy>();

        services.AddHttpClient(TseHttpClient.Name, (provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<TseOptions>>().Value;

            client.BaseAddress = new Uri(settings.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        services.AddSingleton<TseFileDownloader>()
                .AddSingleton<TseArchiveExtractor>()
                .AddSingleton<ITseDatasetAcquirer, TseDatasetAcquirer>()
                .AddSingleton<ITseReferenceReader, TseReferenceReader>()
                .AddSingleton<ITseCandidateReader, TseCandidateReader>()
                .AddSingleton<ITseSectionResultReader, TseSectionResultReader>();

        return services.AddScoped<IImportPipeline, TseReferencePipeline>()
                       .AddScoped<IImportPipeline, TseCandidatePipeline>()
                       .AddScoped<IImportPipeline, TseResultsPipeline>()
                       .AddScoped<DatasetVersionService>()
                       .AddScoped<TseMunicipalityResolver>()
                       .AddScoped<TseSyntheticScopeSeeder>();
    }
}
