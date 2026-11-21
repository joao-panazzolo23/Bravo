using System.ComponentModel.DataAnnotations;

namespace Bravo.Application.Importing.Options;

/// <summary>
/// i dont thinks that needs to be configured but anyways, not bad at all
/// </summary>
public sealed class ImportEngineOptions
{
    public const string SectionName = "Importing";

    [Range(1, 100_000)]
    public int DefaultBatchSize { get; set; } = 1_000;

    [Range(1, 1_000)]
    public int DefaultMaxAttempts { get; set; } = 3;

    [Range(1, 64)]
    public int DefaultMaxParallelism { get; set; } = 64;

    [Range(1, 86_400)]
    public int DefaultLeaseDurationSeconds { get; set; } = 120;

    /// <summary>Minimum delay between persisted progress writes.</summary>
    [Range(250, 60_000)]
    public int ProgressFlushMilliseconds { get; set; } = 1_000;

    [Range(1, 300_000)]
    public int RetryBaseDelayMilliseconds { get; set; } = 500;

    [Range(1, 3_600_000)]
    public int RetryMaxDelayMilliseconds { get; set; } = 30_000;
}
