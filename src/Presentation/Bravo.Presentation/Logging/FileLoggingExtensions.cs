using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Bravo.Presentation.Logging;

public static class FileLoggingExtensions
{
    /// <summary>
    /// Adds the rolling file logger alongside the existing console logger.
    /// </summary>
    /// <remarks>
    /// Console output is untouched, so <c>journalctl -u bravo-api -f</c> keeps working while the
    /// same records are also written to disk for post-restart inspection.
    /// </remarks>
    public static ILoggingBuilder AddRollingFile(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(FileLoggingOptions.SectionName)
            .Get<FileLoggingOptions>() ?? new FileLoggingOptions();

        if (!options.Enabled)
            return builder;

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(new FileLoggerProvider(options)));

        return builder;
    }
}
