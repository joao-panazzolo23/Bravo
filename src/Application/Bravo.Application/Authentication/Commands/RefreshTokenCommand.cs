using Bravo.Domain.Auth.ViewModel;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Commands;

public record RefreshTokenCommand : ICommand<Result<LoginViewModel>>
{
    public required string RefreshToken { get; set; }
}
