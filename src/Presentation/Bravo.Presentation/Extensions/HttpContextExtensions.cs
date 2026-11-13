namespace Bravo.Presentation.Extensions;

internal static class HttpContextExtensions
{
    internal static Guid GetUserId(this HttpContext context)
    {
        var id = context.User.Identity.Name;
        return new Guid(id);
    }
}
