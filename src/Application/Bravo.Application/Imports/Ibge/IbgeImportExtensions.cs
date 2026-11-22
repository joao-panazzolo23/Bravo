using Bravo.Application.Imports.Ibge.Services;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Imports.Ibge;

/// <summary>
/// Registers the IBGE import module.
/// </summary>
/// <remarks>
/// The source facade is registered as an <see cref="IImportSource"/> so the executor can resolve it
/// by key. It is scoped because every job execution resolves a fresh one from its own scope; a
/// longer lifetime would let one job's state leak into the next.
/// </remarks>
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
