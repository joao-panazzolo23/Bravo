namespace Bravo.Domain.Auth.ViewModel;

public record LoginViewModel
{
    public LoginViewModel(string token, string refreshToken)
    {
        Token = token;
        RefreshToken = refreshToken;
    }

    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
