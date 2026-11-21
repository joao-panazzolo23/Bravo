using Bravo.Domain.Importing.Enums;

namespace Bravo.Domain.Importing.Models;

public sealed class ImportExecutionContext(
    Guid jobId,
    int batchSize,
    int maxParallelism,
    IProgress<ImportExecutionContext.OperationProgress> progress)
{
    public Guid JobId { get; } = jobId;
    public int BatchSize { get; } = batchSize;
    public int MaxParallelism { get; } = maxParallelism;
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
