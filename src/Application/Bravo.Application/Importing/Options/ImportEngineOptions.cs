using System.ComponentModel.DataAnnotations;

namespace Bravo.Application.Importing.Options;

public sealed class ImportEngineOptions
{
    public const string SectionName = "Importing";

    [Range(1, 100_000)]
    public int DefaultBatchSize { get; set; } = 1_000;

    [Range(1, 1_000)]
    public int DefaultMaxAttempts { get; set; } = 5;

    [Range(1, 86_400)]
    public int DefaultLeaseDurationSeconds { get; set; } = 120;

    [Range(1, 300_000)]
    public int RetryBaseDelayMilliseconds { get; set; } = 500;

    [Range(1, 3_600_000)]
    public int RetryMaxDelayMilliseconds { get; set; } = 30_000;
}
