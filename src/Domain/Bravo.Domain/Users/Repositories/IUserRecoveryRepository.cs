using Bravo.Domain.Users.Entities;

namespace Bravo.Domain.Users.Repositories;

public interface IUserRecoveryRepository
{
    Task Create(UserRecovery userRecovery);
    Task<UserRecovery?> GetByTokenHash(string tokenHash);
    Task<IEnumerable<UserRecovery>> ListPendingByUser(Guid userId, DateTime now);
}
