using Bravo.Shared.Result;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Users.ViewModel;
using Mediator;
using Bravo.Application.Users.Queries;

namespace Bravo.Application.Users.QueryHandlers;

public class ListUserHandler(IUserQueryRepository userQueryRepository) : IQueryHandler<ListUserQuery, Result<IEnumerable<ListUserViewModel>>>
{
    private readonly IUserQueryRepository _userQueryRepository = userQueryRepository;

    async ValueTask<Result<IEnumerable<ListUserViewModel>>> IQueryHandler<ListUserQuery, Result<IEnumerable<ListUserViewModel>>>.Handle(ListUserQuery query, CancellationToken cancellationToken)
    {
        var list = await _userQueryRepository.List(query.Page, query.PageSize);
        return Result<IEnumerable<ListUserViewModel>>.OK(list);
    }
}
