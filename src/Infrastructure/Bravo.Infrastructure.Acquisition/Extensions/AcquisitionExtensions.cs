using System.Text.Json;
using Bravo.Application.Importing.Contracts;
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

namespace Bravo.Infrastructure.Acquisition.Extensions;

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

        services.AddScoped<IImportSource<State>, IbgeStateSource>();
        services.AddScoped<IImportRecordValidator<State>, StateValidator>();
        services.AddScoped<IImportPipeline>(provider => new ImportBatchPipeline<State>(
            IbgeStateSource.ImportType,
            provider.GetRequiredService<IImportSource<State>>(),
            provider.GetRequiredService<IImportRecordValidator<State>>(),
            provider.GetRequiredService<IImportBatchStore<State>>(),
            provider.GetRequiredService<ILogger<ImportBatchPipeline<State>>>()));

        return services;
    }
}
