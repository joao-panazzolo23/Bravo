using System.IO.Compression;
using Bravo.Application.Logging;
using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Extraction;

/// <summary>
/// Extracts ZIP archives with explicit defenses against path traversal, zip bombs,
/// entry-count abuse and oversized payloads.
/// </summary>
internal sealed class TseArchiveExtractor(
    IOptions<TseOptions> options,
    ILogger<TseArchiveExtractor> logger)
{
    private readonly TseOptions _options = options.Value;

    public IReadOnlyList<ExtractedFile> Extract(
        string archivePath,
        string destinationDirectory,
        Func<string, bool>? entryFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (!File.Exists(archivePath))
            throw new TseArchiveException($"Archive '{archivePath}' does not exist.");

        Directory.CreateDirectory(destinationDirectory);

        using var archive = OpenArchive(archivePath);

        if (archive.Entries.Count > _options.MaxExtractedEntries)
        {
            throw new TseArchiveException(
                $"Archive contains {archive.Entries.Count} entries, exceeding the limit of {_options.MaxExtractedEntries}.");
        }

        var root = Path.GetFullPath(destinationDirectory);
        var extracted = new List<ExtractedFile>(archive.Entries.Count);
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                continue;

            if (entryFilter is not null && !entryFilter(entry.FullName))
                continue;

            if (entry.Length > _options.MaxExtractedBytes)
            {
                throw new TseArchiveException(
                    $"Entry '{entry.FullName}' declares {entry.Length} bytes, exceeding the limit of {_options.MaxExtractedBytes}.");
            }

            if (entry.CompressedLength > 0)
            {
                var ratio = (double)entry.Length / entry.CompressedLength;

                if (ratio > _options.MaxCompressionRatio)
                {
                    throw new TseArchiveException(
                        $"Entry '{entry.FullName}' has a compression ratio of {ratio:F1}, exceeding the limit of {_options.MaxCompressionRatio}.");
                }
            }

            var target = ResolveSafePath(root, entry.FullName, archivePath);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            var written = ExtractEntry(entry, target);

            totalBytes += written;

            if (totalBytes > _options.MaxExtractedBytes)
            {
                throw new TseArchiveException(
                    $"Archive extraction exceeded the total limit of {_options.MaxExtractedBytes} bytes.");
            }

            ProcessingLog.EntryExtracted(logger, entry.FullName, written);

            extracted.Add(new ExtractedFile(target, entry.FullName, written));
        }

        return extracted;
    }

    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException exception)
        {
            throw new TseArchiveException($"Archive '{archivePath}' is not a valid ZIP file.", exception);
        }
    }

    private string ResolveSafePath(string root, string entryName, string archivePath)
    {
        if (Path.IsPathRooted(entryName))
        {
            throw new TseArchiveException(
                $"Archive '{archivePath}' contains the rooted entry '{entryName}'.");
        }

        var combined = Path.GetFullPath(Path.Combine(root, entryName));

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new TseArchiveException(
                $"Archive '{archivePath}' contains an entry escaping the destination: '{entryName}'.");
        }

        return combined;
    }

    private long ExtractEntry(ZipArchiveEntry entry, string target)
    {
        using var source = entry.Open();
        using var destination = new FileStream(
            target,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024);

        var buffer = new byte[64 * 1024];
        long written = 0;

        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);

            if (read == 0)
                break;

            written += read;

            // Guards against a lying header: stop as soon as the real payload blows the budget.
            if (written > _options.MaxExtractedBytes)
            {
                throw new TseArchiveException(
                    $"Entry '{entry.FullName}' expanded beyond the limit of {_options.MaxExtractedBytes} bytes.");
            }

            destination.Write(buffer, 0, read);
        }

        return written;
    }
}
