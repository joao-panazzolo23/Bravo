using Bravo.Shared.Result;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Users.ViewModel;
using Bravo.Application.Users.Queries;
using Mediator;

namespace Bravo.Application.Users.QueryHandlers;

public class GetUserHandler : IQueryHandler<GetUserQuery, Result<GetUserViewModel>>
{
    private readonly IUserQueryRepository _userQueryRepository;

    public GetUserHandler(IUserQueryRepository userQueryRepository)
    {
        _userQueryRepository = userQueryRepository;
    }

    async ValueTask<Result<GetUserViewModel>> IQueryHandler<GetUserQuery, Result<GetUserViewModel>>.Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var vm = await _userQueryRepository.GetById(query.Id);
        return Result<GetUserViewModel>.OK(vm);
    }
}
