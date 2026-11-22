using Bravo.Application.Imports.Common.Execution;
using Bravo.Application.Imports.Common.Options;
using Bravo.Application.Imports.Common.Progress;
using Bravo.Domain.Importing.Enums;
using Bravo.Domain.Importing.Entities;
using Bravo.Domain.Importing.Models;
using Bravo.Domain.Importing.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.Importing;

/// <summary>
/// Pins the progress sink's two contracts: a redundant snapshot is not written again, and a
/// rejected-record count reaches the job row so the executor can settle as
/// <c>CompletedWithErrors</c> instead of claiming a clean import.
/// </summary>
public sealed class ImportProgressSinkTests
{
    private sealed class RecordingJobs : IImportJobRepository
    {
        public List<ImportJobProgress> Writes { get; } = [];

        public Task<bool> ReportProgressAsync(
            Guid id,
            string workerId,
            ImportJobProgress progress,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(progress);

            return Task.FromResult(true);
        }

        public Task<bool> CreateAsync(ImportJob job, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListRecoverableIdsAsync(
            int limit,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ImportJob?> ClaimAsync(
            Guid id,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RenewLeaseAsync(
            Guid id,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CompleteAsync(
            Guid id,
            string workerId,
            ImportJobStatus status,
            string? error,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task FailAsync(
            Guid id,
            string workerId,
            string error,
            DateTime? retryAt,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReleaseAsync(
            Guid id,
            string workerId,
            string? reason,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> ReleaseOwnedByAsync(
            string ownerPrefix,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static ImportProgressSink CreateSink(IImportJobRepository jobs)
        => new(
            jobs,
            Guid.CreateVersion7(),
            "worker",
            Options.Create(new ImportEngineOptions { ProgressFlushMilliseconds = 250 }),
            NullLogger.Instance);

    private static ImportExecutionContext.OperationProgress Snapshot(
        long processed = 10,
        long failed = 0,
        string? operation = "Candidates 2022")
        => new(
            ImportStage.Candidates,
            TotalOperations: 17,
            CompletedOperations: 1,
            FailedOperations: 0,
            ProcessedRecords: processed,
            FailedRecords: failed,
            CurrentOperation: operation);

    [Fact]
    public async Task TheSameSnapshotIsNotWrittenTwice()
    {
        var jobs = new RecordingJobs();
        var sink = CreateSink(jobs);

        sink.Report(Snapshot());
        await sink.FlushAsync(CancellationToken.None);
        await sink.FlushAsync(CancellationToken.None);

        // The heartbeat flushes on a timer. Without this guard every tick would rewrite the same
        // counters and make ProgressUpdatedAt mean "last heartbeat" instead of "last change".
        jobs.Writes.Should().ContainSingle();
    }

    [Fact]
    public async Task AChangedSnapshotIsWritten()
    {
        var jobs = new RecordingJobs();
        var sink = CreateSink(jobs);

        sink.Report(Snapshot(processed: 10));
        await sink.FlushAsync(CancellationToken.None);

        sink.Report(Snapshot(processed: 20));
        await sink.FlushAsync(CancellationToken.None);

        jobs.Writes.Should().HaveCount(2);
        jobs.Writes[^1].ProcessedRecords.Should().Be(20);
    }

    [Fact]
    public async Task RejectedRecordsReachTheJobRow()
    {
        var jobs = new RecordingJobs();
        var sink = CreateSink(jobs);

        // A scope that persisted 500 rows and rejected 7 unresolvable ones.
        sink.Report(Snapshot(processed: 500, failed: 7));
        await sink.FlushAsync(CancellationToken.None);

        jobs.Writes.Should().ContainSingle();
        jobs.Writes[0].FailedRecords.Should().Be(7);
    }

    [Fact]
    public async Task AFlushWithNoReportDoesNotWrite()
    {
        var jobs = new RecordingJobs();
        var sink = CreateSink(jobs);

        await sink.FlushAsync(CancellationToken.None);

        jobs.Writes.Should().BeEmpty();
    }
}
