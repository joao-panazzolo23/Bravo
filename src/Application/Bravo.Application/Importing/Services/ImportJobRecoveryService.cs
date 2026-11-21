using Bravo.Application.Logging;
using Bravo.Domain.Importing.Repositories;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Startup recovery for jobs a previous process left running.
/// </summary>
/// <remarks>
/// A job whose lease owner starts with this machine's prefix can never be running any more, so
/// releasing it immediately is both safe and much faster than waiting for the lease to expire.
/// </remarks>
public sealed class ImportJobRecoveryService(
    IImportJobRepository jobs,
    ILogger<ImportJobRecoveryService> logger)
{
    public async Task<int> RecoverAbandonedAsync(
        string machineName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineName);

        try
        {
            var released = await jobs.ReleaseOwnedByAsync($"{machineName}:", cancellationToken);

            if (released > 0)
                WorkerLog.WorkerAbandonedJobsRecovered(logger, released, machineName);

            return released;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WorkerLog.WorkerReleaseAbandonedFailed(logger, exception);

            return 0;
        }
    }
}
