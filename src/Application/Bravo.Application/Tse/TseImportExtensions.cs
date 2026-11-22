using Bravo.Application.Imports.Tse.Services;
using Bravo.Application.Tse.Services;
using Bravo.Domain.Importing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Tse;

/// <summary>
/// Registers the TSE import module.
/// </summary>
/// <remarks>
/// Only orchestration lives here. Acquisition, archive extraction, CSV parsing and the HTTP client
/// are registered by <c>Bravo.Infrastructure.Tse</c>, which implements the abstractions this
/// module consumes. Registering the facade here keeps the dependency pointing from Application to
/// its own types, not the reverse.
///
/// Everything is scoped: a stage importer must not retain mutable state across jobs, and the
/// executor creates a fresh scope per execution.
/// </remarks>
public static class TseImportExtensions
{
    public static IServiceCollection AddTseImport(this IServiceCollection services)
    {
        // The session is the job-scoped handoff that stops the reference and candidate stages
        // from downloading the same archive twice. It must be scoped so each job gets its own
        // cache and its own cleanup set.
        return services.AddScoped<TseImportSession>()
                       .AddScoped<TseScopeResolver>()
                       .AddScoped<TseReferenceImporter>()
                       .AddScoped<TseCandidateImporter>()
                       .AddScoped<TseResultImporter>()
                       .AddScoped<TseStructurePreparer>()
                       .AddScoped<DatasetVersionService>()
                       .AddScoped<TseMunicipalityResolver>()
                       .AddScoped<TseSyntheticScopeSeeder>()
                       .AddScoped<TseImportSource>()
                       .AddScoped<IImportSource>(
                           provider => provider.GetRequiredService<TseImportSource>());
    }
}
