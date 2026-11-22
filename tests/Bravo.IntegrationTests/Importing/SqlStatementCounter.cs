using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bravo.IntegrationTests.Importing;

/// <summary>
/// Counts the SQL statements a repository sends through one <see cref="NpgsqlDataSource"/>.
/// </summary>
/// <remarks>
/// Npgsql emits a debug event immediately before it sends each command, so this measures what
/// actually reached the wire rather than how many times the application called a method. That
/// distinction is the point of the identity change: a batch must cost a fixed number of
/// statements regardless of how many identities it contains.
/// </remarks>
public sealed class SqlStatementCounter : ILoggerProvider
{
    /// <summary>Npgsql's "Executing command" event on the <c>Npgsql.Command</c> category.</summary>
    private const int ExecutingCommandEventId = 2000;

    private const string MessagePrefix = "Executing command: ";

    private readonly ConcurrentQueue<string> _statements = new();

    public IReadOnlyList<string> Statements => _statements.ToArray();

    public int Count => _statements.Count;

    /// <summary>Statements that write data, excluding the temporary staging setup.</summary>
    public IReadOnlyList<string> Writes => _statements
        .Where(statement => !statement.StartsWith("CREATE TEMP TABLE", StringComparison.OrdinalIgnoreCase)
                            && !statement.StartsWith("COPY ", StringComparison.OrdinalIgnoreCase)
                            && !statement.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public void Reset() => _statements.Clear();

    public ILogger CreateLogger(string categoryName) => new CommandLogger(categoryName, _statements);

    public void Dispose()
    {
    }

    /// <summary>
    /// Builds a data source whose Npgsql commands are counted into this instance. A dedicated
    /// source keeps the count scoped to one repository call instead of every connection in the
    /// test host.
    /// </summary>
    public NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(this);
        });

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseLoggerFactory(factory);

        return builder.Build();
    }

    private sealed class CommandLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id != ExecutingCommandEventId)
                return;

            var message = formatter(state, exception);

            sink.Enqueue(message.StartsWith(MessagePrefix, StringComparison.Ordinal)
                ? message[MessagePrefix.Length..]
                : message);
        }
    }
}
