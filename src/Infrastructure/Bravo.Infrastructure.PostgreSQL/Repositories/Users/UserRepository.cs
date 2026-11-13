using Microsoft.EntityFrameworkCore;
using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Users.Entities;
using Bravo.Infrastructure.PostgreSQL.Context;

namespace Bravo.Infrastructure.PostgreSQL.Repositories.Users;

internal sealed class UserRepository(PostgreSQLContext context) : IUserRepository
{
    private readonly PostgreSQLContext _context = context;

    public async Task Create(User user)
        => await _context.Users.AddAsync(user);

    public async Task<User?> GetById(Guid id)
        => await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

    public async Task Update(User user)
        => _context.Entry(user).State = EntityState.Modified;

    public async Task Delete(User user) =>
        _context.Users.Remove(user);

    public async Task<User?> GetByEmail(string email)
        => await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
}
