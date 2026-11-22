using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.Application.Imports;

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
