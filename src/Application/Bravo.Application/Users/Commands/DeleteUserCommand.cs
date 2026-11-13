using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Users.Commands;

public record DeleteUserCommand : IRequest<Result<Unit>>
{
    public Guid Id { get; set; }
}