using Bravo.Domain.Importing.Enums;

namespace Bravo.Domain.Importing.Models;

/// <summary>
/// What an import source needs from the job it is executing: settings, cancellation and a
/// place to report progress.
/// </summary>
/// <remarks>
/// The worker supplies one instance per execution. Progress is reported through
/// <see cref="IProgress{T}"/>, which forwards to the job row; sources never touch persistence
/// for progress.
/// </remarks>
public sealed class ImportExecutionContext(
    Guid jobId,
    int batchSize,
    int maxParallelism,
    IProgress<ImportExecutionContext.OperationProgress> progress)
{
    public Guid JobId { get; } = jobId;

    /// <summary>Rows per write batch, used by sources that buffer records.</summary>
    public int BatchSize { get; } = batchSize;

    /// <summary>Upper bound for a stage's own parallelism. Sources must not exceed it.</summary>
    public int MaxParallelism { get; } = maxParallelism;

    /// <summary>
    /// Publishes an absolute progress snapshot. Concurrent operations may call this at any
    /// time; the sink keeps the latest values rather than applying deltas.
    /// </summary>
    public void Report(
        ImportStage stage,
        int totalOperations,
        int completedOperations,
        int failedOperations,
        long processedRecords,
        long failedRecords,
        string? currentOperation) =>
        progress.Report(new OperationProgress(
            stage,
            totalOperations,
            completedOperations,
            failedOperations,
            processedRecords,
            failedRecords,
            currentOperation));

    public sealed record OperationProgress(
        ImportStage Stage,
        int TotalOperations,
        int CompletedOperations,
        int FailedOperations,
        long ProcessedRecords,
        long FailedRecords,
        string? CurrentOperation);
}
