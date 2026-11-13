using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Authentication.Commands;

public record ResetPasswordCommand : ICommand<Result<Unit>>
{
    public required string Token { get; set; }
    public required string NewPassword { get; set; }
}
