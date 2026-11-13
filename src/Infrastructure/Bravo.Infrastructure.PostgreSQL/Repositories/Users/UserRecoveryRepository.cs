using Microsoft.EntityFrameworkCore;
using Bravo.Domain.Users.Entities;
using Bravo.Domain.Users.Repositories;
using Bravo.Infrastructure.PostgreSQL.Context;

namespace Bravo.Infrastructure.PostgreSQL.Repositories.Users;

internal sealed class UserRecoveryRepository(PostgreSQLContext context) : IUserRecoveryRepository
{
    public async Task Create(UserRecovery userRecovery)
        => await context.UserRecoveries.AddAsync(userRecovery);

    public async Task<UserRecovery?> GetByTokenHash(string tokenHash)
        => await context.UserRecoveries.SingleOrDefaultAsync(x => x.TokenHash == tokenHash);

    public async Task<IEnumerable<UserRecovery>> ListPendingByUser(Guid userId, DateTime now)
        => await context.UserRecoveries
                        .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > now)
                        .ToListAsync();
}
