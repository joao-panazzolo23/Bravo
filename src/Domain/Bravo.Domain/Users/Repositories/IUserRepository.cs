using Bravo.Domain.Users.Entities;

namespace Bravo.Domain.Users.Repositories;

public interface IUserRepository
{
    Task<User?> GetById(Guid id);
    Task Create(User user);
    Task Update(User user);
    Task Delete(User user);
    Task<User?> GetByEmail(string email);
}
