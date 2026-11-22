using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Imports.Common;

/// <summary>
/// Registers the execution infrastructure shared by every import source.
/// </summary>
/// <remarks>
/// This module owns the durable job lifecycle: starting, claiming, heartbeating, progress,
/// retry classification and recovery. It knows nothing about IBGE or TSE, so adding a source
/// never requires touching it.
///
/// The <c>ImportResourceGovernor</c> is a singleton on purpose: its limits only mean something if
/// every job execution in the process shares one instance.
/// </remarks>
public static class ImportExecutionExtensions
{
    public static IServiceCollection AddImportExecution(this IServiceCollection services)
    {
        return services.AddScoped<ImportJobStarter>()
                       .AddScoped<ImportRetryPolicy>()
                       .AddScoped<ImportJobExecutor>()
                       .AddScoped<ImportJobRecoveryService>()
                       .AddScoped<IImportJobScheduler, ImportJobScheduler>()
                       .AddSingleton<ImportResourceGovernor>();
    }
}
