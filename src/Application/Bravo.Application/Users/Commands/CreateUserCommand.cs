using Mediator;
using Bravo.Shared.Result;
using Bravo.Domain.Auth.Enums;

namespace Bravo.Application.Users.Commands;

public record CreateUserCommand : ICommand<Result<Unit>>
{
    required public string Name { get; set; }

    required public string Email { get; set; }

    required public string Document { get; set; }

    required public string Phone { get; set; }

    public string Password { get; set; }

    public RoleType Role { get; set; } = RoleType.User;

}
