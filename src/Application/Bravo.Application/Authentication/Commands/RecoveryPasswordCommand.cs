using Mediator;
using Bravo.Shared.Result;

namespace Bravo.Application.Authentication.Commands;

public record RecoveryPasswordCommand : ICommand<Result<Unit>>
{
    public required string Email { get; set; }
}
