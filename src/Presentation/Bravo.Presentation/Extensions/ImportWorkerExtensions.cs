using Bravo.Application.Imports.Common.Options;
using Bravo.Presentation.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Presentation.Extensions;

public static class ImportWorkerExtensions
{
    public static IServiceCollection AddImportWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ImportEngineOptions>()
            .Bind(configuration.GetSection(ImportEngineOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ImportWorkerOptions>()
            .Bind(configuration.GetSection(ImportWorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<ImportWorker>();

        return services;
    }
}
