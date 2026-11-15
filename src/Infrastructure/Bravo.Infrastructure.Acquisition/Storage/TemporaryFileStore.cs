using System.Text.Json;
using Bravo.Application.Acquisition.Options;
using Bravo.Application.Acquisition.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Acquisition.Storage;

/// <summary>
/// File-system temporary storage. Every execution writes into its own directory so a cleanup
/// never removes files still in use by another execution.
/// </summary>
public sealed class TemporaryFileStore(
    IOptions<AcquisitionOptions> options,
    ILogger<TemporaryFileStore> logger) : ITemporaryFileStore
{
    private const string MetadataExtension = ".meta.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _root = options.Value.TempDirectory;
    private readonly Lock _lock = new();

    public string CreatePath(Guid executionId, string datasetKey, string extension)
    {
        var safeKey = Sanitize(datasetKey);
        var directory = Path.Combine(_root, executionId.ToString("N"));
        Directory.CreateDirectory(directory);

        var name = $"{safeKey}-{Guid.NewGuid():N}{extension}";
        return Path.Combine(directory, name);
    }

    public void EnsureRootExists() => Directory.CreateDirectory(_root);

    public CachedDownload? FindVerified(Guid executionId, string datasetKey)
    {
        lock (_lock)
        {
            var directory = Path.Combine(_root, executionId.ToString("N"));
            if (!Directory.Exists(directory))
            {
                return null;
            }

            var safeKey = Sanitize(datasetKey);
            foreach (var sidecar in Directory.EnumerateFiles(directory, $"{safeKey}-*{MetadataExtension}"))
            {
                try
                {
                    var metadata = JsonSerializer.Deserialize<Sidecar>(File.ReadAllText(sidecar), JsonOptions);
                    if (metadata?.FilePath is null || !File.Exists(metadata.FilePath))
                    {
                        continue;
                    }

                    if (new FileInfo(metadata.FilePath).Length != metadata.SizeBytes)
                    {
                        continue;
                    }

                    return new CachedDownload(
                        metadata.FilePath,
                        metadata.SizeBytes,
                        metadata.Sha256,
                        metadata.DownloadedAtUtc,
                        metadata.ETag,
                        metadata.LastModified);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Ignoring unreadable download metadata {Path}.", sidecar);
                }
            }

            return null;
        }
    }

    public void SaveMetadata(string filePath, CachedDownload metadata)
    {
        try
        {
            var sidecar = filePath + MetadataExtension;
            var payload = new Sidecar(
                metadata.FilePath,
                metadata.SizeBytes,
                metadata.Sha256,
                metadata.DownloadedAtUtc,
                metadata.ETag,
                metadata.LastModified);

            File.WriteAllText(sidecar, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write download metadata for {Path}.", filePath);
        }
    }

    public void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var sidecar = path + MetadataExtension;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }

            var directory = Path.GetDirectoryName(path);
            if (directory is not null
                && directory.StartsWith(_root, StringComparison.Ordinal)
                && Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete temporary file {Path}.", path);
        }
    }

    public int CleanupOlderThan(TimeSpan retention)
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        var threshold = DateTime.UtcNow - retention;
        var removed = 0;

        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (File.GetLastWriteTimeUtc(file) < threshold)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not clean temporary directory {Directory}.", directory);
            }
        }

        return removed;
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        return new string(chars);
    }

    private sealed record Sidecar(
        string FilePath,
        long SizeBytes,
        string Sha256,
        DateTime DownloadedAtUtc,
        string? ETag,
        DateTimeOffset? LastModified);
}
