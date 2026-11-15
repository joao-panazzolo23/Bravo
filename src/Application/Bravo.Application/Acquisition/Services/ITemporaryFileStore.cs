namespace Bravo.Application.Acquisition.Services;

/// <summary>
/// Temporary storage for downloaded datasets. Files are streamed to disk rather than held in
/// memory and are deleted once the execution no longer needs them.
///
/// A sidecar metadata file next to each download records its checksum, size and source
/// validators. It lets an interrupted execution reuse an already verified download instead of
/// fetching the same archive twice, without ever parsing a partial file.
/// </summary>
public interface ITemporaryFileStore
{
    /// <summary>
    /// Creates a unique file path for the given execution and dataset key.
    /// </summary>
    string CreatePath(Guid executionId, string datasetKey, string extension);

    /// <summary>
    /// Ensures the configured root directory exists.
    /// </summary>
    void EnsureRootExists();

    /// <summary>
    /// Returns a previously verified download for the same execution and dataset, when the
    /// file is still present and its checksum matches the recorded metadata.
    /// </summary>
    CachedDownload? FindVerified(Guid executionId, string datasetKey);

    /// <summary>
    /// Records the metadata of a completed download next to it.
    /// </summary>
    void SaveMetadata(string filePath, CachedDownload metadata);

    /// <summary>
    /// Deletes a file if it exists. Failures are logged, never thrown.
    /// </summary>
    void TryDelete(string? path);

    /// <summary>
    /// Removes files whose last write is older than the retention window.
    /// </summary>
    int CleanupOlderThan(TimeSpan retention);
}

/// <summary>
/// Metadata of a downloaded file, used for provenance and safe reuse.
/// </summary>
public record CachedDownload(
    string FilePath,
    long SizeBytes,
    string Sha256,
    DateTime DownloadedAtUtc,
    string? ETag = null,
    DateTimeOffset? LastModified = null);
