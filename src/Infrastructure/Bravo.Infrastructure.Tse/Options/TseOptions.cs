namespace Bravo.Infrastructure.Tse.Options;

public sealed class TseOptions
{
    public const string SectionName = "Tse";

    public string BaseAddress { get; set; } = "https://cdn.tse.jus.br/";

    public int HttpTimeoutSeconds { get; set; } = 300;

    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryBaseDelayMs { get; set; } = 500;

    public int RetryMaxDelayMs { get; set; } = 30_000;

    public string TempDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "bravo-tse");

    public long MaxDownloadBytes { get; set; } =
        10L * 1024 * 1024 * 1024;

    public long MaxExtractedBytes { get; set; } =
        8L * 1024 * 1024 * 1024;

    public int MaxExtractedEntries { get; set; } = 1_000;

    public int MaxCompressionRatio { get; set; } = 200;
}
