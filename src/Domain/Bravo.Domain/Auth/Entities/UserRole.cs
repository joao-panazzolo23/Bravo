using Bravo.Domain.Users.Entities;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Auth.Entities;

public class UserRole : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; }
    public Guid RoleId { get; private set; }
    public Role Role { get; set; }
}
