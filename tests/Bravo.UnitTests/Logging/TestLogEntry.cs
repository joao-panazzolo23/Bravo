using Microsoft.Extensions.Logging;

namespace Bravo.UnitTests.Logging;

public sealed record TestLogEntry(
    LogLevel LogLevel,
    EventId EventId,
    Exception? Exception,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyList<object?> Scopes);
