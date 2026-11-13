using Bravo.Domain.Auth.Enums;

namespace Bravo.Domain.Users.ViewModel;

public record GetUserViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Document { get; set; }
    public DateTime? DismissalDate { get; set; }
    public string Phone { get; set; }
    public RoleType RoleType { get; set; }
    public bool IsActive { get; set; }
}
