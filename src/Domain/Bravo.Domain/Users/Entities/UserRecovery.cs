using Bravo.Shared.Entities;
using Bravo.Shared.Exceptions;

namespace Bravo.Domain.Users.Entities;

public class UserRecovery : Entity
{
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public Guid UserId { get; private set; }
    public User? User { get; private set; }

    public UserRecovery(
        Guid userId,
        string tokenHash,
        DateTime expiresAt
    )
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsValid(DateTime now) => UsedAt is null && ExpiresAt > now;

    public void Consume(DateTime now)
    {
        if (!IsValid(now))
            throw new DomainException("Token de recuperação inválido ou expirado.");

        UsedAt = now;
    }

    public void Revoke(DateTime now) => UsedAt ??= now;
}
