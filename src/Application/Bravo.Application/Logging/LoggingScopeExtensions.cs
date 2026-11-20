using Microsoft.Extensions.Logging;

namespace Bravo.Application.Logging;

public static class LoggingScopeExtensions
{
    public static IDisposable? BeginJobScope(
        this ILogger logger,
        Guid jobId,
        string source,
        string? workerId = null,
        string? correlationId = null)
    {
        return logger.BeginScope(new JobScope(jobId, source, workerId, correlationId));
    }

    public static IDisposable? BeginWorkUnitScope(
        this ILogger logger,
        Guid workUnitId,
        int? year = null,
        string? state = null,
        string? correlationId = null)
    {
        return logger.BeginScope(new WorkUnitScope(workUnitId, year, state, correlationId));
    }

    public static IDisposable? BeginOperationScope(
        this ILogger logger,
        int? year = null,
        string? state = null,
        string? workerId = null,
        string? correlationId = null)
    {
        return logger.BeginScope(new OperationScope(year, state, workerId, correlationId));
    }
}
