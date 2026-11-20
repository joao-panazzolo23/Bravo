using System.ComponentModel.DataAnnotations;

namespace Bravo.Presentation.Workers;

public sealed class ImportWorkerOptions
{
    public const string SectionName = "Importing:Worker";

    public bool Enabled { get; set; } = true;

    [Range(1, 3_600)]
    public int PollIntervalSeconds { get; set; } = 5;

    [Range(1, 100)]
    public int MaxConcurrentJobs { get; set; } = 2;

    [Range(1, 1_000)]
    public int MaxJobsPerPoll { get; set; } = 20;

    /// <summary>
    /// When true, jobs left Running by a previous process on this machine are released at
    /// startup instead of waiting for their lease to expire.
    /// </summary>
    public bool RecoverOnStartup { get; set; } = true;
}
