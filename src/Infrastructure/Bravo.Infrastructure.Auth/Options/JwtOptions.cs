using System.ComponentModel.DataAnnotations;

namespace Bravo.Infrastructure.Auth.Options;

internal sealed record JwtOptions
{
    [Required]
    public required string Issuer { get; init; }
    [Required]
    public required string Audience { get; init; }
    [Required]
    [MinLength(32)]
    public required string AccessKey { get; init; }
    [Required]
    [MinLength(32)]
    public required string RefreshKey { get; init; }
    [Range(1, 30)]
    public int RefreshTokenExpireDays { get; init; }
    [Range(1, 60)]
    public int TokenExpireMinutes { get; init; }

    public TimeSpan AccessTokenLifetime =>
      TimeSpan.FromMinutes(TokenExpireMinutes);

    public TimeSpan RefreshTokenLifetime =>
        TimeSpan.FromDays(RefreshTokenExpireDays);
}
