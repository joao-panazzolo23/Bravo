using Bravo.Domain.Auth.ValueObjects;

namespace Bravo.Domain.Auth.Services;

public interface IRecoveryService
{
    RecoveryToken Generate();
    string Hash(string token);
}
