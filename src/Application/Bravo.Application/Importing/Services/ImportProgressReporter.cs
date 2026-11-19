using System.Diagnostics;
using Bravo.Domain.Importing.Services;
using Bravo.Domain.Importing.Enums;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Throttled progress reporter for a single work unit.
/// </summary>
/// <remarks>
/// <para>
/// Counters are held locally and written as absolute values, so concurrent work units never
/// share mutable state and a retried flush cannot double-count.
/// </para>
/// <para>
/// A write happens at most once per <c>interval</c> or once per <c>recordThreshold</c> records,
/// whichever comes first, so throughput is not dominated by observability.
/// </para>
/// </remarks>
public sealed class ImportProgressReporter : IImportProgressReporter
{
    private readonly Func<ImportProgressSnapshot, CancellationToken, Task> _persist;
    private readonly ILogger _logger;
    private readonly TimeSpan _interval;
    private readonly long _recordThreshold;
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _gate = new();

    private ImportStage _stage = ImportStage.Pending;
    private long _processed;
    private long _inserted;
    private long _updated;
    private long _rejected;
    private long? _totalRecords;
    private long _lastFlushedProcessed;
    private TimeSpan _lastFlush = TimeSpan.MinValue;

    public ImportProgressReporter(
        Func<ImportProgressSnapshot, CancellationToken, Task> persist,
        ILogger logger,
        TimeSpan? interval = null,
        long recordThreshold = 100_000)
    {
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? TimeSpan.FromSeconds(5);
        _recordThreshold = recordThreshold;
    }

    public async Task StageAsync(ImportStage stage, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _stage = stage;
        }

        var snapshot = await SnapshotAndMarkFlushedAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {ImportStage}. ElapsedSeconds: {ElapsedSeconds:F1}, ProcessedRecords: {ProcessedRecords}.",
            stage, snapshot.Elapsed.TotalSeconds, snapshot.ProcessedRecords);
    }

    public async Task RecordsAsync(
        long processed,
        long inserted,
        long updated,
        long rejected,
        long? totalRecords = null,
        CancellationToken cancellationToken = default)
    {
        bool due;

        lock (_gate)
        {
            _processed = processed;
            _inserted = inserted;
            _updated = updated;
            _rejected = rejected;

            if (totalRecords is not null)
                _totalRecords = totalRecords;

            due = _stopwatch.Elapsed - _lastFlush >= _interval
                  || processed - _lastFlushedProcessed >= _recordThreshold;
        }

        if (!due)
            return;

        var snapshot = await SnapshotAndMarkFlushedAsync(cancellationToken);

        _logger.LogInformation(
            "Progress {ImportStage}. ProcessedRecords: {ProcessedRecords}, InsertedRecords: {InsertedRecords}, UpdatedRecords: {UpdatedRecords}, RejectedRecords: {RejectedRecords}, ElapsedSeconds: {ElapsedSeconds:F1}, RecordsPerSecond: {RecordsPerSecond:F0}.",
            snapshot.Stage,
            snapshot.ProcessedRecords,
            snapshot.InsertedRecords,
            snapshot.UpdatedRecords,
            snapshot.RejectedRecords,
            snapshot.Elapsed.TotalSeconds,
            snapshot.RecordsPerSecond);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
        => SnapshotAndMarkFlushedAsync(cancellationToken);

    private async Task<ImportProgressSnapshot> SnapshotAndMarkFlushedAsync(CancellationToken cancellationToken)
    {
        await _flushGate.WaitAsync(cancellationToken);

        try
        {
            ImportProgressSnapshot snapshot;

            lock (_gate)
            {
                snapshot = SnapshotUnsafe();
                _lastFlush = _stopwatch.Elapsed;
                _lastFlushedProcessed = _processed;
            }

            await _persist(snapshot, cancellationToken);

            return snapshot;
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private ImportProgressSnapshot SnapshotUnsafe()
    {
        var elapsed = _stopwatch.Elapsed;

        var rate = elapsed.TotalSeconds > 0.5
            ? _processed / elapsed.TotalSeconds
            : (double?)null;

        return new ImportProgressSnapshot(
            _stage,
            _processed,
            _inserted,
            _updated,
            _rejected,
            _totalRecords,
            elapsed,
            rate);
    }
}
