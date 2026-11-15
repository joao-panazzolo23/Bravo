using System.IO.Compression;

namespace Bravo.Infrastructure.Acquisition.Storage;

/// <summary>
/// Safe access to downloaded archives. Validation happens before any entry is streamed:
/// the archive must be a readable ZIP, every entry must stay inside the archive, and the
/// declared decompressed size must not exceed the configured limit. This protects against
/// malformed archives and decompression bombs.
/// </summary>
public static class ArchiveGuard
{
    /// <summary>
    /// Validates the ZIP structure and the total declared decompressed size.
    /// Returns the CSV entries eligible for parsing.
    /// </summary>
    public static IReadOnlyList<ZipArchiveEntry> GetCsvEntries(
        ZipArchive archive,
        long maxDecompressedBytes,
        Func<ZipArchiveEntry, bool>? predicate = null)
    {
        long total = 0;
        var entries = new List<ZipArchiveEntry>();

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(entry.FullName))
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' escapes the archive root.");
            }

            if (entry.Length < 0 || entry.Length > maxDecompressedBytes)
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' declares {entry.Length} bytes, above the configured limit.");
            }

            total += entry.Length;
            if (total > maxDecompressedBytes)
            {
                throw new InvalidDataException(
                    $"Archive declares {total} decompressed bytes, above the configured limit of {maxDecompressedBytes}.");
            }

            var isCsv = entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        || entry.FullName.EndsWith(".CSV", StringComparison.OrdinalIgnoreCase);

            if (isCsv && (predicate is null || predicate(entry)))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// Opens a bounded reading stream over an entry, so a corrupt header cannot make the
    /// reader consume more bytes than the entry declares.
    /// </summary>
    public static Stream OpenBounded(ZipArchiveEntry entry, long maxBytes)
    {
        var stream = entry.Open();
        return new BoundedReadStream(stream, Math.Min(entry.Length, maxBytes));
    }
}

internal sealed class BoundedReadStream(Stream inner, long limit) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => limit;
    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var allowed = (int)Math.Min(count, limit - _read);
        if (allowed <= 0)
        {
            return 0;
        }

        var read = inner.Read(buffer, offset, allowed);
        _read += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var allowed = (int)Math.Min(count, limit - _read);
        if (allowed <= 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(buffer, offset, allowed, cancellationToken);
        _read += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var allowed = (int)Math.Min(buffer.Length, limit - _read);
        if (allowed <= 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(buffer[..allowed], cancellationToken);
        _read += read;
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
