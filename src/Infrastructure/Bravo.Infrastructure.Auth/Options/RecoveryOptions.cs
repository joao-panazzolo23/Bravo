using System.ComponentModel.DataAnnotations;

namespace Bravo.Infrastructure.Auth.Options;

internal sealed record RecoveryOptions
{
    [Range(5, 60)]
    public int TokenExpireMinutes { get; init; } = 30;

    public TimeSpan TokenLifetime =>
        TimeSpan.FromMinutes(TokenExpireMinutes);
}
