using Bravo.Shared.Entities;

namespace Bravo.Domain.Auth.Entities;

public class UserClaim : Entity
{
    public Guid UserId { get; private set; }
    public Guid ClaimId { get; private set; }
}

