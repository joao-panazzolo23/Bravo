using Bravo.Domain.Auth.ValueObjects;
using Bravo.Domain.Users.Entities;

namespace Bravo.Domain.Services.Email;

public interface IAccountEmailService
{
    Task SendPasswordRecovery(User user, RecoveryToken token, CancellationToken cancellationToken = default);
    Task SendPasswordChanged(User user, CancellationToken cancellationToken = default);
}
