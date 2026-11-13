using Bravo.Domain.Auth.Enums;

namespace Bravo.Presentation.Authorization;

internal static class AuthorizationRoles
{
    public const string Administrators = $"{nameof(RoleType.Master)},{nameof(RoleType.Admin)}";
}
