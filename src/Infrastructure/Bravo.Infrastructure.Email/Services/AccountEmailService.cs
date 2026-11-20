using Bravo.Domain.Auth.ValueObjects;
using Bravo.Domain.Services.Email;
using Bravo.Domain.Users.Entities;
using Bravo.Infrastructure.Email.DTO;
using Bravo.Infrastructure.Email.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Bravo.Infrastructure.Email.Services;

internal sealed class AccountEmailService(
    IEmailService emailService,
    IOptions<AccountEmailOptions> options,
    ILogger<AccountEmailService> logger
    ) : IAccountEmailService
{
    private readonly AccountEmailOptions _options = options.Value;

    public async Task SendPasswordRecovery(User user, RecoveryToken token, CancellationToken cancellationToken = default)
    {
        var link = $"{_options.ResetPasswordUrl}#token={token.Value}";

        var html = $"""
            <p>Olá, {WebUtility.HtmlEncode(user.Name)}.</p>
            <p>Recebemos uma solicitação para redefinir a sua senha.</p>
            <p><a href="{link}">Redefinir senha</a></p>
            <p>Este link expira em {token.ExpiresAt:dd/MM/yyyy HH:mm} (UTC) e só pode ser usado uma vez.</p>
            <p>Se você não fez esta solicitação, ignore este e-mail. Sua senha atual continua válida.</p>
            """;

        await Send(user, "Recuperação de senha", html, cancellationToken);
    }

    public async Task SendPasswordChanged(User user, CancellationToken cancellationToken = default)
    {
        var html = $"""
            <p>Olá, {WebUtility.HtmlEncode(user.Name)}.</p>
            <p>A senha da sua conta foi alterada e todas as sessões foram encerradas.</p>
            <p>Se não foi você, entre em contato com o suporte imediatamente.</p>
            """;

        await Send(user, "Sua senha foi alterada", html, cancellationToken);
    }

    private async Task Send(User user, string subject, string html, CancellationToken cancellationToken)
    {
        try
        {
            await emailService.Send(user.Email, subject, html, cancellationToken);
        }
        catch (Exception exception)
        {
            AccountEmailLog.EmailSendFailed(logger, subject, user.Id, exception);
        }
    }
}
