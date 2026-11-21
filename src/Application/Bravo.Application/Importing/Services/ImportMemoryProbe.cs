using System.Diagnostics;
using Bravo.Application.Logging;
using Microsoft.Extensions.Logging;

namespace Bravo.Application.Importing.Services;

/// <summary>
/// Logs a memory snapshot at import stage boundaries.
/// </summary>
/// <remarks>
/// This is deliberately coarse: a snapshot per stage is enough to spot unbounded growth without
/// turning observability into its own allocation source. It reads the runtime's own counters and
/// never forces a collection, because a sampling GC would distort the very numbers it reports.
/// </remarks>
public static class ImportMemoryProbe
{
    public static void LogSnapshot(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var info = GC.GetGCMemoryInfo();

        ResourceLog.ImportMemorySnapshot(
            logger,
            GC.GetTotalMemory(forceFullCollection: false) / 1048576.0,
            Process.GetCurrentProcess().WorkingSet64 / 1048576.0,
            info.HeapSizeBytes / 1048576.0,
            GC.CollectionCount(2));
    }
}
