using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed class TestLogger : ILogger
{
    private static readonly AsyncLocal<ImmutableStack<object?>> CurrentScope = new();
    private readonly ConcurrentBag<TestLogEntry> _entries = new();

    public IReadOnlyList<TestLogEntry> Entries => _entries.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var previous = CurrentScope.Value ?? ImmutableStack<object?>.Empty;
        CurrentScope.Value = previous.Push(state);
        return new ScopeDisposable(previous);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        var scopes = (CurrentScope.Value ?? ImmutableStack<object?>.Empty).ToArray();
        _entries.Add(new TestLogEntry(logLevel, eventId, exception, message, properties, scopes));
    }

    private sealed class ScopeDisposable(ImmutableStack<object?> previous) : IDisposable
    {
        public void Dispose() => CurrentScope.Value = previous;
    }
}
