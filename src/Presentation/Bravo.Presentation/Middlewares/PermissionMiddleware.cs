using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Bravo.Presentation.Middlewares;
/// <summary>
/// This interface is used to handle results when authorization fails.
/// You could return different StatusCodes depending on what you want to show user.
/// Sometimes, you want to deny the existance of an actual resource instead of
/// saying user is not authorized to use it.
/// </summary>
public class PermissionMiddleware(
    ) : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        throw new NotImplementedException();
    }

    //public async Task InvokeAsync(HttpContext context)
    //{
    //    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

    //    if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
    //    {
    //        var user = await repository.GetById(userId);
    //        user.UserRoles.SelectMany(x => x.RoleType);
    //        context.Items["UserPermissions"] = permissions;
    //    }

    //    await next(context);
    //}

}
