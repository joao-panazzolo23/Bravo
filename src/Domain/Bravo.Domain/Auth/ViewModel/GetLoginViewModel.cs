namespace Bravo.Domain.Auth.ViewModel;

public record GetLoginViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
