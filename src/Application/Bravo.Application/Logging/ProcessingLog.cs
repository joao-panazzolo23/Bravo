using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static partial class ProcessingLog
{
    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Debug,
        Message = "Download started for '{Url}'.")]
    public static partial void DownloadStarted(
        ILogger logger,
        string url);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Information,
        Message = "Download completed for '{Url}'. Size: {SizeBytes} bytes, SHA256: {Sha256}.")]
    public static partial void DownloadCompleted(
        ILogger logger,
        string url,
        long sizeBytes,
        string sha256);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "Transient download failure for '{Url}'; retrying in {DelayMs} ms (attempt {Attempt}/{MaxAttempts}).")]
    public static partial void DownloadRetrying(
        ILogger logger,
        string url,
        double delayMs,
        int attempt,
        int maxAttempts,
        Exception exception);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Error,
        Message = "Download failed for '{Url}' after {MaxAttempts} attempt(s).")]
    public static partial void DownloadFailed(
        ILogger logger,
        string url,
        int maxAttempts,
        Exception exception);

    [LoggerMessage(
        EventId = 1205,
        Level = LogLevel.Debug,
        Message = "Extracting archive '{ArchivePath}' to '{TargetDirectory}'.")]
    public static partial void ExtractionStarted(
        ILogger logger,
        string archivePath,
        string targetDirectory);

    [LoggerMessage(
        EventId = 1206,
        Level = LogLevel.Debug,
        Message = "Archive extraction completed for '{ArchivePath}'. Total files: {FileCount}, Total bytes: {TotalBytes}.")]
    public static partial void ExtractionCompleted(
        ILogger logger,
        string archivePath,
        int fileCount,
        long totalBytes);

    [LoggerMessage(
        EventId = 1207,
        Level = LogLevel.Debug,
        Message = "Extracted '{Entry}' ({Bytes} bytes).")]
    public static partial void EntryExtracted(
        ILogger logger,
        string entry,
        long bytes);

    [LoggerMessage(
        EventId = 1208,
        Level = LogLevel.Debug,
        Message = "Parsing started for '{FilePath}' using layout '{LayoutVersion}'.")]
    public static partial void ParsingStarted(
        ILogger logger,
        string filePath,
        string layoutVersion);

    [LoggerMessage(
        EventId = 1209,
        Level = LogLevel.Debug,
        Message = "Parsing completed for '{FilePath}'. Total records read: {RecordCount}.")]
    public static partial void ParsingCompleted(
        ILogger logger,
        string filePath,
        int recordCount);

    [LoggerMessage(
        EventId = 1210,
        Level = LogLevel.Information,
        Message = "Removed stale workspace '{Directory}'.")]
    public static partial void WorkspaceCleaned(
        ILogger logger,
        string directory);

    [LoggerMessage(
        EventId = 1211,
        Level = LogLevel.Information,
        Message = "Import stage '{Stage}' started with {TotalOperations} operation(s).")]
    public static partial void StageStarted(
        ILogger logger,
        string stage,
        int totalOperations);

    [LoggerMessage(
        EventId = 1212,
        Level = LogLevel.Information,
        Message = "Import stage '{Stage}' completed. Processed: {ProcessedRecords}, Failed: {FailedOperations}.")]
    public static partial void StageCompleted(
        ILogger logger,
        string stage,
        long processedRecords,
        int failedOperations);

    [LoggerMessage(
        EventId = 1220,
        Level = LogLevel.Error,
        Message = "TSE reference import failed for {Year}; continuing with other years.")]
    public static partial void ReferenceImportFailed(
        ILogger logger,
        int year,
        Exception exception);

    [LoggerMessage(
        EventId = 1221,
        Level = LogLevel.Error,
        Message = "TSE candidate import failed for {Year}; continuing with other years.")]
    public static partial void CandidateImportFailed(
        ILogger logger,
        int year,
        Exception exception);

    [LoggerMessage(
        EventId = 1222,
        Level = LogLevel.Error,
        Message = "TSE results import failed for {Year}/{State}; continuing with other scopes.")]
    public static partial void ResultScopeImportFailed(
        ILogger logger,
        int year,
        string state,
        Exception exception);
}
