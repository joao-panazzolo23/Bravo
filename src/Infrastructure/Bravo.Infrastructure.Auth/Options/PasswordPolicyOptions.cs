using System.ComponentModel.DataAnnotations;

namespace Bravo.Infrastructure.Auth.Options;

internal sealed record PasswordPolicyOptions
{
    [Range(1, 128)]
    public int MinLength { get; init; } = 8;

    /// <summary>
    /// Upper bound also protects the entropy estimation and the hashing from DoS.
    /// </summary>
    [Range(1, 1024)]
    public int MaxLength { get; init; } = 128;

    [Range(0, 4)]
    public int MinEntropyScore { get; init; } = 3;

    public bool RequireUppercase { get; init; } = true;

    public bool RequireLowercase { get; init; } = true;

    public bool RequireDigit { get; init; } = true;

    public bool RequireSpecialCharacter { get; init; } = true;
}
