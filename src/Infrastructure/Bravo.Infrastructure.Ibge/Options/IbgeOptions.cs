namespace Bravo.Infrastructure.Ibge.Options;

public class IbgeOptions
{
    public int MaxConcurrentRequests { get; set; } = 2;
    public int MinDelayBetweenRequestsMs { get; set; } = 500;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 500;
    public int HttpTimeoutSeconds { get; set; } = 120;
    public string TempDirectory { get; set; } = Path.GetTempPath();
}
