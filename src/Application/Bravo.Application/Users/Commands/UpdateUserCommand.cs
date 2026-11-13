namespace Bravo.Application.Users.Commands;

public record UpdateUserCommand : CreateUserCommand
{
    public Guid Id { get; set; }
    public string? Password { get; set; }
    public bool? IsActive { get; set; }
}
