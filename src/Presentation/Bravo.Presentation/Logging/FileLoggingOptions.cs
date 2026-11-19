using System.ComponentModel.DataAnnotations;

namespace Bravo.Presentation.Logging;

/// <summary>
/// Options for the built-in rolling file logger.
/// </summary>
public sealed class FileLoggingOptions
{
    public const string SectionName = "Logging:File";

    public bool Enabled { get; set; } = true;

    /// <summary>Directory that receives the log files. Created when missing.</summary>
    public string Directory { get; set; } = "logs";

    public string FileName { get; set; } = "bravo-.log";

    /// <summary>Minimum level written to file. Defaults to Information.</summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>Maximum size of a single file before rolling.</summary>
    [Range(1024, 1024L * 1024 * 1024)]
    public long MaxFileBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>Number of retained rolled files. Older files are deleted.</summary>
    [Range(1, 100)]
    public int RetainedFiles { get; set; } = 10;
}
