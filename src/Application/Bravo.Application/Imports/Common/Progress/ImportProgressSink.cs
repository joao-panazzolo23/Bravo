using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Logging;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Application.Imports.Common.Progress;

/// <summary>
/// Thread-safe, throttled bridge between concurrently executing work and the job row.
/// </summary>
/// <remarks>
/// Sources report absolute counters from several concurrent operations. The sink keeps the
/// latest snapshot and always writes absolute values, so a delayed write can never resurrect an
/// older count. Writes are serialized; a flush that finds a write in progress retries instead of
/// dropping its snapshot, because the final flush is what makes the job's counters correct.
/// A write failure is logged and never fails the import: progress is observability.
///
/// A snapshot identical to the one already persisted is not written again. The heartbeat calls
/// <see cref="FlushAsync"/> on a fixed interval, and without this guard every tick would rewrite
/// the same counters, making "ProgressUpdatedAt" mean "last heartbeat" instead of "last change".
/// The heartbeat's own liveness signal is the lease renewal, which is independent and never
/// suppressed.
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
    private ImportExecutionContext.OperationProgress? _lastPersisted;
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

                    // The previous flush already wrote exactly these counters. Returning keeps a
                    // steady-state heartbeat from producing an UPDATE with no new information.
                    if (_pending == _lastPersisted)
                        return;

                    snapshot = _pending;
                    _lastWrite = DateTime.UtcNow;
                }

                if (!await PersistAsync(snapshot, cancellationToken))
                    return;

                lock (_stateGate)
                {
                    _lastPersisted = snapshot;

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

    /// <summary>
    /// Writes one snapshot, returning whether it reached the database. A failure is logged and
    /// reported as not persisted so the next flush retries it instead of treating it as written.
    /// </summary>
    private async Task<bool> PersistAsync(
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

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PersistenceLog.ProgressPersistFailed(logger, jobId, exception);

            return false;
        }
    }
}
