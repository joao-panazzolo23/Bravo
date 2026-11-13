using Bravo.Shared.Entities;

namespace Bravo.Domain.Auth.Entities;

public class RolePermission() : Entity
{
    public RolePermission(Guid roleId) : this()
    {
        this.RoleId = roleId;
    }
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; }
    public ICollection<Permission> Permissions { get; private set; } = [];
}
