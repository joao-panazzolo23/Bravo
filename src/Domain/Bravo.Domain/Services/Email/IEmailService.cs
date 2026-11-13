namespace Bravo.Domain.Services.Email;

public interface IEmailService
{
    Task Send(string to, string subject, string html, CancellationToken token = default);
}
