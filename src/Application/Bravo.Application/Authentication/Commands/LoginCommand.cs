using Bravo.Domain.Auth.ViewModel;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Commands;

public record LoginCommand : ICommand<Result<LoginViewModel>>
{
    public string? Email { get; set; }
    public string? Password { get; set; }

}
