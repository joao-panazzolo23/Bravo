using Bravo.Domain.Auth.Entities;
using Bravo.Domain.Auth.Enums;
using Bravo.Shared.Entities;

namespace Bravo.Domain.Users.Entities;

public class User : Entity
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string Document { get; private set; }
    public string Phone { get; private set; }
    public string Password { get; private set; }
    public RoleType RoleType { get; private set; } = RoleType.User;
    public IEnumerable<UserRole> UserRoles { get; set; } = [];
    public bool IsActive { get; private set; } = true;
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    public User(
        string name,
        string email,
        string document,
        string phone,
        string password,
        RoleType roleType = RoleType.User
        )
    {
        Name = name;
        Email = email;
        Document = document;
        Phone = phone;
        Password = password;
        RoleType = roleType;
    }

    public void ChangePassword(string passwordHash)
    {
        Password = passwordHash;
        SecurityStamp = Guid.NewGuid();
    }

    public void RehashPassword(string passwordHash) => Password = passwordHash;

    public void Update(
        string name,
        string email,
        string document,
        string phone,
        bool? isActive,
        RoleType? roleType = null)
    {
        Name = name;
        Email = email;
        Document = document;
        Phone = phone;

        if (isActive is not null)
        {
            IsActive = (bool)isActive;
        }

        if (roleType is not null)
        {
            RoleType = roleType.Value;
        }
    }
}
