using Bravo.Domain.Services.Email;
using Bravo.Infrastructure.Email.DTO;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bravo.Infrastructure.Email.Services;

internal sealed class EmailService(IOptions<EmailOptions> options) : IEmailService
{
    /// <summary>
    /// Todo: Test email service.
    /// I updated to 2.x.x to 4.15.1, it may cause some issues, but I don't think so. 
    /// </summary>
    /// <param name="to"></param>
    /// <param name="subject"></param>
    /// <param name="html"></param>
    /// <returns></returns>

    private readonly EmailOptions _options = options.Value;
    public async Task Send(string to, string subject, string html, CancellationToken token = default)
    {
        var message = await ConfigureMime(to, subject, html);
        var smtp = await OpenSMTPAsync(_options, token);

        await smtp.SendAsync(message, token);
        await smtp.DisconnectAsync(true, token);
    }

    private async Task<MimeMessage> ConfigureMime(string to, string subject, string html)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.Email));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = html
        }.ToMessageBody();

        return message;
    }

    private static async Task<SmtpClient> OpenSMTPAsync(EmailOptions _options, CancellationToken token)
    {
        var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.Security,
             token

        );

        await smtp.AuthenticateAsync(
           _options.User,
           _options.Pass,
           token
       );

        return smtp;
    }

}
