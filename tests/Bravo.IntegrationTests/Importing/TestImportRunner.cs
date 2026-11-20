using Bravo.Application.Importing.Options;
using Bravo.Application.Importing.Services;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Brave = Bravo.Domain.Importing.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Executes one claim→run→complete cycle using the real worker algorithm but without the
/// BackgroundService timer, so tests stay deterministic.
/// </summary>
public sealed class TestImportRunner(
    IImportJobRepository jobs,
    IEnumerable<Brave.IImportSource> sources,
    IOptions<ImportEngineOptions> options,
    ILogger<TestImportRunner> logger)
{
    public async Task<Bravo.Domain.Importing.Entities.ImportJob?> RunAsync(
        Guid jobId,
        string workerId = "worker-test")
    {
        var settings = options.Value;
        var lease = TimeSpan.FromSeconds(settings.DefaultLeaseDurationSeconds);

        var job = await jobs.ClaimAsync(jobId, workerId, lease);

        if (job is null)
            return await jobs.GetAsync(jobId);

        var source = sources.FirstOrDefault(candidate => candidate.Key == job.Source);

        if (source is null)
        {
            await jobs.CompleteAsync(
                job.Id, workerId, ImportJobStatus.Failed,
                $"No import source is registered for '{job.Source}'.");

            return await jobs.GetAsync(job.Id);
        }

        var progress = new ImportProgressSink(
            jobs,
            job.Id,
            workerId,
            Options.Create(settings),
            NullLogger.Instance);

        var context = new ImportExecutionContext(
            job.Id,
            job.BatchSize,
            job.MaxParallelism,
            progress);

        try
        {
            await source.ExecuteAsync(context, CancellationToken.None);
            await progress.FlushAsync(CancellationToken.None);

            var finished = await jobs.GetAsync(job.Id);

            await jobs.CompleteAsync(
                job.Id,
                workerId,
                finished is { TotalFailedRecords: > 0 }
                    ? ImportJobStatus.CompletedWithErrors
                    : ImportJobStatus.Completed,
                error: null);
        }
        catch (OperationCanceledException)
        {
            await jobs.ReleaseAsync(job.Id, workerId, "cancelled");
        }
        catch (Exception exception)
        {
            var retryPolicy = new ImportRetryPolicy(Options.Create(settings));
            var transient = ImportFailureClassifier.IsTransient(exception);

            DateTime? retryAt = null;

            if (transient && job.AttemptCount < job.MaxAttempts)
            {
                retryAt = DateTime.UtcNow.Add(retryPolicy.GetDelay(
                    job.AttemptCount,
                    ImportFailureClassifier.GetRetryAfter(exception)));
            }

            logger.LogDebug(exception, "Test import run failed for {JobId}.", job.Id);

            await jobs.FailAsync(job.Id, workerId, exception.Message, retryAt);
        }

        return await jobs.GetAsync(job.Id);
    }
}
