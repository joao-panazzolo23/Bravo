using Bravo.Application.Importing.Options;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Thread-safe, throttled bridge between concurrently executing work and the job row.
/// </summary>
/// <remarks>
/// Sources report absolute counters from several concurrent operations. The sink keeps the
/// latest snapshot and always writes absolute values, so a delayed write can never resurrect an
/// older count. Writes are serialized; a flush that finds a write in progress retries instead of
/// dropping its snapshot, because the final flush is what makes the job's counters correct.
/// A write failure is logged and never fails the import: progress is observability.
/// </remarks>
public sealed class ImportProgressSink(
    IImportJobRepository jobs,
    Guid jobId,
    string workerId,
    IOptions<ImportEngineOptions> options,
    ILogger logger) : IProgress<ImportExecutionContext.OperationProgress>
{
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(options.Value.ProgressFlushMilliseconds);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _stateGate = new();

    private ImportExecutionContext.OperationProgress? _pending;
    private DateTime _lastWrite = DateTime.MinValue;

    public void Report(ImportExecutionContext.OperationProgress value)
    {
        bool due;

        lock (_stateGate)
        {
            _pending = value;
            due = DateTime.UtcNow - _lastWrite >= _interval;
        }

        if (due)
            _ = FlushAsync(CancellationToken.None);
    }

    /// <summary>
    /// Writes the latest snapshot. Concurrent callers are serialized, and the loop re-reads the
    /// pending value so the most recent report is never lost.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);

        try
        {
            while (true)
            {
                ImportExecutionContext.OperationProgress snapshot;

                lock (_stateGate)
                {
                    if (_pending is null)
                        return;

                    snapshot = _pending;
                    _lastWrite = DateTime.UtcNow;
                }

                await PersistAsync(snapshot, cancellationToken);

                lock (_stateGate)
                {
                    if (ReferenceEquals(_pending, snapshot))
                        return;
                }
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task PersistAsync(
        ImportExecutionContext.OperationProgress snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            await jobs.ReportProgressAsync(
                jobId,
                workerId,
                new ImportJobProgress(
                    snapshot.Stage,
                    snapshot.TotalOperations,
                    snapshot.CompletedOperations,
                    snapshot.FailedOperations,
                    snapshot.ProcessedRecords,
                    snapshot.FailedRecords,
                    snapshot.CurrentOperation),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception, "Could not persist progress for import job {JobId}.", jobId);
        }
    }
}
