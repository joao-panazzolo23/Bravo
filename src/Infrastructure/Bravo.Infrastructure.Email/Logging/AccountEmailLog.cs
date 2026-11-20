using Microsoft.Extensions.Logging;

namespace Bravo.Infrastructure.Email.Logging;

internal static partial class AccountEmailLog
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Falha ao enviar o e-mail '{Subject}' para o usuário {UserId}")]
    public static partial void EmailSendFailed(
        ILogger logger,
        string subject,
        Guid userId,
        Exception exception);
}
