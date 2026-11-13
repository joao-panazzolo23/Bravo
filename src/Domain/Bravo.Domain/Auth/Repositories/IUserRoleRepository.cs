using Bravo.Domain.Auth.Entities;

namespace Bravo.Domain.Auth.Repositories;

public interface IUserRoleRepository
{
    public Task Create(UserRole role);
    public Task Delete(UserRole role);
    public Task<UserRole> GetById(UserRole role);
}

