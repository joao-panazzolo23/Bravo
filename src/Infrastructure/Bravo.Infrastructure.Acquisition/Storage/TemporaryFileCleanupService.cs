using Bravo.Application.Acquisition.Services;
using Bravo.Application.Acquisition.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.Storage;

/// <summary>
/// Removes temporary downloads left behind by failed or interrupted executions. Retention is
/// configurable; successful executions already delete their own files.
/// </summary>
public sealed class TemporaryFileCleanupService(
    ITemporaryFileStore fileStore,
    IOptions<AcquisitionOptions> options,
    ILogger<TemporaryFileCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retention = TimeSpan.FromHours(Math.Max(1, options.Value.TempRetentionHours));
                var removed = fileStore.CleanupOlderThan(retention);
                if (removed > 0)
                {
                    logger.LogInformation("Removed {Count} expired temporary acquisition files.", removed);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Temporary acquisition file cleanup failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
