using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Infrastructure.Acquisition.Http;
using Bravo.Application.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.Persistence;
using Bravo.Infrastructure.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Queue;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Bravo.Infrastructure.Acquisition.Repositories;
using Bravo.Infrastructure.Acquisition.Resilience;
using Bravo.Infrastructure.Acquisition.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paramore.Fences;

namespace Bravo.Infrastructure.Acquisition.Extensions;

public static class AcquisitionExtensions
{
    private const string IbgeKey = "ibge";
    private const string TseKey = "tse";
    private const string FgvKey = "fgv";

    public static IServiceCollection AddAcquisition(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AcquisitionOptions>()
            .Bind(configuration.GetSection(AcquisitionOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<HostRateLimiterRegistry>();
        services.AddSingleton<ITemporaryFileStore, TemporaryFileStore>();

        RegisterProvider(services, IbgeKey, o => o.Ibge);
        RegisterProvider(services, TseKey, o => o.Tse);
        RegisterProvider(services, FgvKey, o => o.Fgv);

        services.AddTransient<IIbgeProvider>(sp =>
            ActivatorUtilities.CreateInstance<IbgeProvider>(
                sp,
                sp.GetRequiredKeyedService<DatasetFetcher>(IbgeKey),
                sp.GetRequiredKeyedService<ProviderOptions>(IbgeKey)));

        services.AddTransient<ITseProvider>(sp =>
            ActivatorUtilities.CreateInstance<TseProvider>(
                sp,
                sp.GetRequiredKeyedService<DatasetFetcher>(TseKey),
                sp.GetRequiredKeyedService<ProviderOptions>(TseKey)));

        services.AddTransient<IFgvProvider>(sp =>
            ActivatorUtilities.CreateInstance<FgvProvider>(
                sp,
                sp.GetRequiredKeyedService<DatasetFetcher>(FgvKey),
                sp.GetRequiredKeyedService<ProviderOptions>(FgvKey)));

        services.AddScoped<IGeographyPersistenceService, GeographyPersistenceService>();
        services.AddScoped<IElectoralPersistenceService, ElectoralPersistenceService>();
        services.AddScoped<IElectoralResultsPersistenceService, ElectoralResultsPersistenceService>();

        services.AddSingleton<ImportExecutionQueue>();
        services.AddSingleton<IImportExecutionQueue>(sp => sp.GetRequiredService<ImportExecutionQueue>());

        services.AddScoped<IImportExecutionRepository, ImportExecutionRepository>();
        services.AddScoped<IImportCheckpointRepository, ImportCheckpointRepository>();
        services.AddScoped<IImportRejectionRepository, ImportRejectionRepository>();
        services.AddScoped<IDatasetProvenanceRepository, DatasetProvenanceRepository>();
        services.AddScoped<ImportExecutionRunner>();

        services.AddHostedService<ImportBackgroundService>();
        services.AddHostedService<TemporaryFileCleanupService>();

        return services;
    }

    private static void RegisterProvider(
        IServiceCollection services,
        string key,
        Func<AcquisitionOptions, ProviderOptions> selector)
    {
        services.AddKeyedSingleton(key, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptions<AcquisitionOptions>>().Value;
            return selector(options);
        });

        services.AddKeyedSingleton(key, (sp, _) =>
        {
            var policy = sp.GetRequiredKeyedService<ProviderOptions>(key);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger($"Acquisition.{key}");
            return ProviderResiliencePipelineFactory.Create(policy, logger);
        });

        services.AddHttpClient(key, (sp, client) =>
        {
            var policy = sp.GetRequiredKeyedService<ProviderOptions>(key);
            client.BaseAddress = new Uri(policy.BaseAddress);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", policy.UserAgent);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddKeyedTransient(key, (sp, _) => new DatasetFetcher(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(key),
            sp.GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>(key),
            sp.GetRequiredService<HostRateLimiterRegistry>(),
            sp.GetRequiredService<ITemporaryFileStore>(),
            sp.GetRequiredKeyedService<ProviderOptions>(key),
            sp.GetRequiredService<IOptions<AcquisitionOptions>>().Value,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger($"Acquisition.{key}")));
    }
}
