namespace Bravo.Application.Acquisition.Options;

/// <summary>
/// Configuration of the acquisition subsystem. Every external source has its own policy:
/// rate limits are never assumed to be equal across providers.
/// </summary>
public class AcquisitionOptions
{
    public const string SectionName = "Acquisition";

    /// <summary>
    /// Root directory for downloaded datasets. Must be writable by the API process.
    /// </summary>
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "bravo-acquisition");

    /// <summary>
    /// How long an unprocessed download is retained before cleanup. Failed or interrupted
    /// executions keep their files for this window to allow diagnosis.
    /// </summary>
    public int TempRetentionHours { get; set; } = 24;

    /// <summary>
    /// Maximum size of a single downloaded file. Enforced while streaming.
    /// </summary>
    public long MaxDownloadBytes { get; set; } = 1_500_000_000;

    /// <summary>
    /// Maximum size of one decompressed archive entry. Enforced before extraction and while
    /// streaming the entry.
    /// </summary>
    public long MaxDecompressedBytes { get; set; } = 4_000_000_000;

    /// <summary>
    /// Maximum number of independent files downloaded concurrently by one import execution.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 2;

    public int GeographyBatchSize { get; set; } = 1000;
    public int CandidateBatchSize { get; set; } = 2000;
    public int ResultBatchSize { get; set; } = 5000;
    public int StatisticsBatchSize { get; set; } = 5000;

    /// <summary>
    /// Maximum number of rejection samples persisted per dataset and execution.
    /// </summary>
    public int MaxRejectionSamples { get; set; } = 500;

    public ProviderOptions Ibge { get; set; } = ProviderOptions.ForIbge();
    public ProviderOptions Tse { get; set; } = ProviderOptions.ForTse();
    public ProviderOptions Fgv { get; set; } = ProviderOptions.ForFgv();

    public ProviderOptions Get(string provider) => provider switch
    {
        "IBGE" => Ibge,
        "TSE" => Tse,
        "FGV" => Fgv,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider.")
    };
}

public class ProviderOptions
{
    public string BaseAddress { get; set; } = string.Empty;
    public int MaxConcurrentRequests { get; set; } = 2;
    public int MinDelayBetweenRequestsMs { get; set; } = 500;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 500;
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Hosts the provider is allowed to fetch from. A URL resolved by an external catalog
    /// must still match this list, which protects against SSRF through catalog injection.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    public string UserAgent { get; set; } = "Bravo-ElectoralAnalysis/1.0";

    public static ProviderOptions ForIbge() => new()
    {
        BaseAddress = "https://servicodados.ibge.gov.br/",
        MaxConcurrentRequests = 2,
        MinDelayBetweenRequestsMs = 500,
        AllowedHosts = ["servicodados.ibge.gov.br"]
    };

    public static ProviderOptions ForTse() => new()
    {
        BaseAddress = "https://dadosabertos.tse.jus.br/",
        MaxConcurrentRequests = 2,
        MinDelayBetweenRequestsMs = 1000,
        AllowedHosts = ["dadosabertos.tse.jus.br", "cdn.tse.jus.br"]
    };

    public static ProviderOptions ForFgv() => new()
    {
        BaseAddress = "https://raw.githubusercontent.com/",
        MaxConcurrentRequests = 1,
        MinDelayBetweenRequestsMs = 1000,
        AllowedHosts = ["raw.githubusercontent.com", "github.com"]
    };
}
