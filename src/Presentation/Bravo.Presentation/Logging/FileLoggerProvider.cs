using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Bravo.Presentation.Logging;

/// <summary>
/// Writes formatted log lines to a size-rolling file set.
/// </summary>
/// <remarks>
/// <para>
/// Implemented on top of the built-in <see cref="ILoggerProvider"/> so the application keeps a
/// single logging stack; no additional logging framework is introduced.
/// </para>
/// <para>
/// A single lock serialises writes, which is acceptable because import progress is reported per
/// batch rather than per record. Files roll at <see cref="FileLoggingOptions.MaxFileBytes"/> and
/// the oldest are deleted beyond <see cref="FileLoggingOptions.RetainedFiles"/> so disk usage
/// stays bounded.
/// </para>
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly FileLoggingOptions _options;
    private readonly LogLevel _minimumLevel;

    private StreamWriter? _writer;
    private long _currentBytes;
    private int _currentIndex;
    private DateOnly _currentDate;

    public FileLoggerProvider(FileLoggingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _minimumLevel = Enum.TryParse<LogLevel>(options.MinimumLevel, ignoreCase: true, out var level)
            ? level
            : LogLevel.Information;

        System.IO.Directory.CreateDirectory(_options.Directory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private bool IsEnabled(LogLevel level) => level >= _minimumLevel && level != LogLevel.None;

    private void Write(string line)
    {
        lock (_gate)
        {
            EnsureWriter(line.Length);

            _writer!.WriteLine(line);

            _currentBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        }
    }

    private void EnsureWriter(int pendingLength)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var needsNewFile = _writer is null
                           || _currentDate != today
                           || _currentBytes + pendingLength >= _options.MaxFileBytes;

        if (!needsNewFile)
            return;

        _writer?.Flush();
        _writer?.Dispose();

        if (_currentDate != today)
            _currentIndex = 0;

        _currentDate = today;

        var path = BuildPath(today, _currentIndex);

        _writer = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            // Progress lines must be tail-able while the import is running, so each record is
            // flushed immediately. Import logging is batch-level, so this is not a hot path.
            AutoFlush = true
        };

        _currentBytes = new FileInfo(path).Exists ? new FileInfo(path).Length : 0;

        PruneOldFiles();

        _currentIndex++;
    }

    private string BuildPath(DateOnly date, int index)
        => Path.Combine(
            _options.Directory,
            $"{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{index:D3}-{_options.FileName}");

    private void PruneOldFiles()
    {
        try
        {
            var files = System.IO.Directory
                .GetFiles(_options.Directory, $"*-{_options.FileName}")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .Skip(_options.RetainedFiles)
                .ToArray();

            foreach (var file in files)
                File.Delete(file);
        }
        catch (IOException)
        {
            // Retention is best-effort; never fail logging because a file could not be deleted.
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var line = new StringBuilder()
                .Append(timestamp)
                .Append(" [")
                .Append(Level(logLevel))
                .Append("] ")
                .Append(category)
                .Append(": ")
                .Append(formatter(state, exception));

            if (exception is not null)
                line.Append(Environment.NewLine).Append(exception);

            provider.Write(line.ToString());
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
    }
}
