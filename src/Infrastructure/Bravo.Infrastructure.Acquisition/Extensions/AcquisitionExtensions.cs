using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Acquisition.Repositories;
using Bravo.Application.Acquisition.Services;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.Persistence;
using Bravo.Infrastructure.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Queue;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Bravo.Infrastructure.Acquisition.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Infrastructure.Acquisition.Extensions;

public static class AcquisitionExtensions
{
    public static IServiceCollection AddAcquisition(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IbgeOptions>()
            .Bind(configuration.GetSection("Acquisition:Ibge"));

        services.AddHttpClient("ibge", client =>
        {
            client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            client.DefaultRequestHeaders.Add("User-Agent", "Bravo-ElectoralAnalysis/1.0");
        });

        services.AddSingleton<IbgeRateLimiter>();
        services.AddSingleton<ImportExecutionQueue>();
        services.AddSingleton<IImportExecutionQueue>(sp => sp.GetRequiredService<ImportExecutionQueue>());

        services.AddScoped<IImportExecutionRepository, ImportExecutionRepository>();
        services.AddScoped<IImportCheckpointRepository, ImportCheckpointRepository>();
        services.AddScoped<IDatasetProvenanceRepository, DatasetProvenanceRepository>();
        services.AddScoped<IIbgeProvider, IbgeProvider>();
        services.AddScoped<IGeographyPersistenceService, GeographyPersistenceService>();

        services.AddHostedService<ImportBackgroundService>();

        return services;
    }
}

