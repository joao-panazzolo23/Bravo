namespace Bravo.Domain.Auth.ViewModel;

public record ListLoginViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; }
}
