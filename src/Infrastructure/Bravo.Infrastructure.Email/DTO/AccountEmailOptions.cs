using System.ComponentModel.DataAnnotations;

namespace Bravo.Infrastructure.Email.DTO;

internal sealed record AccountEmailOptions
{
    [Required]
    [Url]
    public required string ResetPasswordUrl { get; init; }
}
