using Bravo.Application.Ibge;
using Bravo.Application.Imports.Ibge.Services;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Imports.Ibge;

public static class IbgeImportExtensions
{
    public static IServiceCollection AddIbgeImport(this IServiceCollection services)
    {
        services.AddScoped<IbgeReferenceImporter>();
        services.AddScoped<IbgeImportSource>();
        services.AddScoped<IImportSource>(
            provider => provider.GetRequiredService<IbgeImportSource>());

        return services;
    }
}
