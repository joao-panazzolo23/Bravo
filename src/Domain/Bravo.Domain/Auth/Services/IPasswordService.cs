using Bravo.Domain.Auth.Enums;

namespace Bravo.Domain.Auth.Services;

public interface IPasswordService
{
    string Hash(string password);
    PasswordVerification Verify(string passwordHash, string password);
}
