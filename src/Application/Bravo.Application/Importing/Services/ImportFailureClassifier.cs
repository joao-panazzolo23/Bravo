using System.Data.Common;
using System.Net.Sockets;
using Bravo.Application.Importing.Exceptions;

namespace Bravo.Application.Importing.Services;

public static class ImportFailureClassifier
{
    public static bool IsTransient(Exception exception)
        => exception switch
        {
            ImportTransientException => true,
            ImportPermanentException => false,
            ImportLeaseLostException => false,
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
