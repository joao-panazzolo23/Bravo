using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Policies;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Domain.Importing.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Executes one claim→run→settle cycle through the production <see cref="ImportJobExecutor"/>,
/// without the BackgroundService polling loop, so tests stay deterministic.
/// </summary>
/// <remarks>
/// The executor is resolved from the request scope, exactly as the mediator handler resolves it
/// in production. That means these tests cover the real lifecycle rather than a copy of it.
/// </remarks>
public sealed class TestImportRunner(IServiceProvider services)
{
    public const string TestWorkerId = "worker-test";

    public async Task<ImportJob?> RunAsync(Guid jobId, string workerId = TestWorkerId)
    {
        var executor = services.GetRequiredService<ImportJobExecutor>();

        await executor.ExecuteAsync(jobId, workerId, CancellationToken.None);

        var jobs = services.GetRequiredService<Bravo.Domain.Importing.Repositories.IImportJobRepository>();

        return await jobs.GetAsync(jobId);
    }
}
