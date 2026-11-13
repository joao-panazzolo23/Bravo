using Bravo.Domain.Auth.ValueObjects;
using Bravo.Domain.Users.Entities;

namespace Bravo.Domain.Auth.Services;

public interface ITokenService
{
    string GenerateToken(User user);
    string GenerateRefreshToken(User user);
    RefreshTokenPayload? ValidateRefresh(string token);
}
