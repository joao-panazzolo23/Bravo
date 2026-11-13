using Bravo.Domain.Users.ViewModel;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Users.Queries;

public record GetUserQuery : IQuery<Result<GetUserViewModel>>
{
    public GetUserQuery(Guid id)
    {
        Id = id;
    }
    public Guid Id { get; set; }
}
