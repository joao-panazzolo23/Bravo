using Bravo.Domain.Auth.Services;
using Bravo.Domain.Auth.ValueObjects;
using Bravo.Domain.Users.Entities;
using Bravo.Infrastructure.Auth.Constants;
using Bravo.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Bravo.Infrastructure.Auth.Services;

internal sealed class TokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider
        ) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public string GenerateToken(User user)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AuthClaims.Role, user.RoleType.ToString()),
            new(AuthClaims.TokenUse, AuthClaims.AccessToken)
        ];

        return Write(claims, _options.AccessSigningKey(), _options.AccessTokenLifetime);
    }

    /// <summary>
    /// Disclaimer: Refresh Token must never store authorization claims.
    ///
    /// It only carries the user id and the SecurityStamp, which is rotated when the credentials change.
    /// That makes every refresh token issued before a password change invalid.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public string GenerateRefreshToken(User user)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AuthClaims.SecurityStamp, user.SecurityStamp.ToString()),
            new(AuthClaims.TokenUse, AuthClaims.RefreshToken)
        ];

        return Write(claims, _options.RefreshSigningKey(), _options.RefreshTokenLifetime);
    }

    /// <summary>
    /// SecurityTokenExpiredException => Quando o token já expirou (exp) e ValidateLifetime = true)
    ///SecurityTokenInvalidSignatureException => Quando a assinatura do token não bate com a IssuerSigningKey
    ///SecurityTokenInvalidAudienceException => Se ValidateAudience = true e o aud não corresponde
    ///SecurityTokenInvalidIssuerException => Se ValidateIssuer = true e o iss não corresponde
    ///SecurityTokenNoExpirationException => Se o token não tem role exp e ValidateLifetime = true
    ///SecurityTokenException => Qualquer outro problema com o token(formato inválido, corrupto, etc.)
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public RefreshTokenPayload? ValidateRefresh(string token)
    {
        try
        {
            var parameters = _options.RefreshValidationParameters();
            parameters.LifetimeValidator = ValidateLifetime;

            var principal = _handler.ValidateToken(token, parameters, out _);

            if (principal.FindFirstValue(AuthClaims.TokenUse) != AuthClaims.RefreshToken)
                return null;

            if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
                return null;

            if (!Guid.TryParse(principal.FindFirstValue(AuthClaims.SecurityStamp), out var securityStamp))
                return null;

            return new RefreshTokenPayload(userId, securityStamp);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lifetime is checked against the injected TimeProvider, the same clock used to issue tokens.
    /// </summary>
    private bool ValidateLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        if (expires is null)
            return false;

        var now = timeProvider.GetUtcNow().UtcDateTime;

        return (notBefore is null || notBefore.Value - parameters.ClockSkew <= now)
            && expires.Value + parameters.ClockSkew > now;
    }

    private string Write(IEnumerable<Claim> claims, SecurityKey key, TimeSpan lifetime)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return _handler.WriteToken(token);
    }
}
