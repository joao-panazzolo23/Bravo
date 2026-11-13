using Bravo.Domain.Users.ViewModel;

namespace Bravo.Domain.Users.Repositories;

public interface IUserQueryRepository
{
    Task<GetUserViewModel?> GetById(Guid id);
    Task<IEnumerable<ListUserViewModel?>> List(int page, int pageSize);
    Task<GetUserViewModel?> GetByEmail(string email);
}
