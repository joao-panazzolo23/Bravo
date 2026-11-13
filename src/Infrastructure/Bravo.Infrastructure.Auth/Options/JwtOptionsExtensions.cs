using Bravo.Infrastructure.Auth.Constants;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Bravo.Infrastructure.Auth.Options;

internal static class JwtOptionsExtensions
{
    public static SymmetricSecurityKey AccessSigningKey(this JwtOptions options)
        => new(Encoding.UTF8.GetBytes(options.AccessKey));

    public static SymmetricSecurityKey RefreshSigningKey(this JwtOptions options)
        => new(Encoding.UTF8.GetBytes(options.RefreshKey));

    public static TokenValidationParameters AccessValidationParameters(this JwtOptions options)
        => options.ValidationParameters(options.AccessSigningKey());

    public static TokenValidationParameters RefreshValidationParameters(this JwtOptions options)
        => options.ValidationParameters(options.RefreshSigningKey());

    private static TokenValidationParameters ValidationParameters(this JwtOptions options, SecurityKey key)
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = AuthClaims.Role
        };
}
