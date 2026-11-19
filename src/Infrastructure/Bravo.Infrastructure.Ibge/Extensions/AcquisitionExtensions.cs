using Bravo.Application.Acquisition.Providers;
using Bravo.Application.Importing;
using Bravo.Domain.Importing.Services;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Geography.Entities;
using Bravo.Infrastructure.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.RateLimiting;
using Bravo.Infrastructure.Acquisition.Sources;
using Bravo.Infrastructure.Acquisition.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bravo.Infrastructure.Acquisition;

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
            provider.GetRequiredService<IOptions<IbgeOptions>>().Value));

        services.AddHttpClient(IbgeHttpClient.Name, (provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<IbgeOptions>>().Value;

            client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/");
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        return services.AddStatesImport()
                       .AddMunicipalitiesImport();
    }

    private static IServiceCollection AddStatesImport(this IServiceCollection services)
    {
        return services.AddScoped<IImportSource<State>, IbgeStateSource>()
                       .AddScoped<IImportRecordValidator<State>, StateValidator>()
                       .AddScoped<IImportPipeline>(provider => new ImportBatchPipeline<State>(
                           IbgeImport.StatesUnitKey,
                           provider.GetRequiredService<IImportSource<State>>(),
                           provider.GetRequiredService<IImportRecordValidator<State>>(),
                           provider.GetRequiredService<IImportBatchStore<State>>(),
                           provider.GetRequiredService<ILogger<ImportBatchPipeline<State>>>()));
    }

    private static IServiceCollection AddMunicipalitiesImport(this IServiceCollection services)
    {
        return services.AddScoped<IImportSource<IbgeMunicipalityRecord>, IbgeMunicipalitySource>()
                       .AddScoped<IImportRecordValidator<IbgeMunicipalityRecord>, MunicipalityValidator>()
                       .AddScoped<IImportPipeline>(provider => new ImportBatchPipeline<IbgeMunicipalityRecord>(
                           IbgeImport.MunicipalitiesUnitKey,
                           provider.GetRequiredService<IImportSource<IbgeMunicipalityRecord>>(),
                           provider.GetRequiredService<IImportRecordValidator<IbgeMunicipalityRecord>>(),
                           provider.GetRequiredService<IImportBatchStore<IbgeMunicipalityRecord>>(),
                           provider.GetRequiredService<ILogger<ImportBatchPipeline<IbgeMunicipalityRecord>>>()));
    }
}
