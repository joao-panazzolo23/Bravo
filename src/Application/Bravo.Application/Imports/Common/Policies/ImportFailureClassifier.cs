using System.Data.Common;
using System.Net.Sockets;
using Bravo.Application.Imports.Common.Exceptions;

namespace Bravo.Application.Imports.Common.Policies;

/// <summary>
/// Decides whether a failed import attempt is worth retrying. Transient failures are retried
/// until the job's attempt budget is spent; everything else fails immediately.
/// </summary>
public static class ImportFailureClassifier
{
    public static bool IsTransient(Exception exception)
        => exception switch
        {
            ImportTransientException => true,
            ImportPermanentException => false,
            HttpRequestException => true,
            SocketException => true,
            DbException databaseException => databaseException.IsTransient,
            TimeoutException => true,
            TaskCanceledException => true,
            IOException => true,
            _ => false
        };

    public static TimeSpan? GetRetryAfter(Exception exception)
        => exception is ImportTransientException { RetryAfter: not null } transient
            ? transient.RetryAfter
            : null;
}
