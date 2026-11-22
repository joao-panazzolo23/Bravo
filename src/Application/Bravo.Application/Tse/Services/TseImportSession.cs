using Bravo.Domain.Electoral.Services;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Job-scoped handoff for acquired TSE candidate archives.
/// </summary>
/// <remarks>
/// The reference stage and the candidate stage both need every year's candidate archive. Without a
/// session each stage downloads and extracts the same ZIP independently, so a job transferred and
/// unpacked the entire candidate dataset twice.
///
/// This type is registered as scoped, so exactly one instance exists per job execution. It caches
/// the <em>paths and metadata</em> of the extracted state files, never their contents: the CSV rows
/// are still streamed from disk by whichever stage needs them. That keeps memory bounded and leaves
/// the per-stage reader permits meaningful.
///
/// Temporary directories are tracked and deleted when the scope is disposed, which happens on
/// success, on failure and on cancellation alike, because the executor always disposes the job's
/// service scope. Concurrent jobs cannot share files: each has its own scope, and the acquirer
/// already writes into a directory named after a fresh identifier.
///
/// Recovery from an abandoned job simply builds a new session; nothing is persisted here.
/// </remarks>
public sealed class TseImportSession(
    ILogger<TseImportSession> logger
) : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<int, IReadOnlyList<AcquiredCandidateFile>> _candidatesByYear = [];
    private readonly HashSet<string> _temporaryDirectories = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _disposed;

    /// <summary>
    /// Returns the extracted candidate files for <paramref name="year"/>, acquiring them only the
    /// first time.
    /// </summary>
    /// <remarks>
    /// The copy is done under one async mutex so two stages cannot race for the same year and both
    /// download. The stages are sequential today, but the guard keeps the invariant local instead of
    /// depending on the caller's ordering.
    /// </remarks>
    public async Task<IReadOnlyList<AcquiredCandidateFile>> GetOrAcquireCandidatesAsync(
        int year,
        ITseDatasetAcquirer acquirer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acquirer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_candidatesByYear.TryGetValue(year, out var cached))
            return cached;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_candidatesByYear.TryGetValue(year, out cached))
                return cached;

            var files = await acquirer.AcquireCandidatesAsync(year, cancellationToken);

            // The acquirer owns the naming of the extraction directory but not its lifetime. The
            // file path is the only handle we get, so the parent is what the session tracks.
            foreach (var file in files)
                TrackDirectory(Path.GetDirectoryName(file.Dataset.FilePath));

            _candidatesByYear[year] = files;

            return files;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Records a directory the session created and must remove, for example the extraction target
    /// of a results archive.
    /// </summary>
    public void TrackDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        _temporaryDirectories.Add(directory);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _gate.Dispose();

        foreach (var directory in _temporaryDirectories)
            DeleteQuietly(directory);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleanup is best effort by design: a failure to delete a temporary directory must never turn
    /// a successful import into a failed one. The operating system will reclaim it eventually.
    /// </summary>
    private void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TseImportSessionLog.TemporaryDirectoryCleanupFailed(logger, directory, exception);
        }
    }
}
