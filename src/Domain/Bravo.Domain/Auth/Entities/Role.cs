using Bravo.Shared.Entities;

namespace Bravo.Domain.Auth.Entities;

public class Role : Entity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public UserRole UserRole { get; set; }
    public IEnumerable<RolePermission> RolePermissions { get; set; }
}
