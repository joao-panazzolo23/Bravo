using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Workspaces;

/// <summary>
/// Owns a disposable temporary directory per download/import attempt so partial work is
/// cleaned up deterministically and never leaks between runs.
/// </summary>
public sealed class TseWorkspace : IDisposable
{
    private bool _disposed;

    public TseWorkspace(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TseWorkspace Create(IOptions<TseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var root = options.Value.TempDirectory;
        Directory.CreateDirectory(root);

        var path = System.IO.Path.Combine(root, $"run-{Guid.CreateVersion7():N}");

        Directory.CreateDirectory(path);

        return new TseWorkspace(path);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void CleanupStale(string root, TimeSpan maxAge, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
            return;

        var cutoff = DateTime.UtcNow - maxAge;

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (Directory.GetCreationTimeUtc(directory) >= cutoff)
                    continue;

                Directory.Delete(directory, recursive: true);
                logger.LogInformation("Removed stale TSE workspace '{Directory}'.", directory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
