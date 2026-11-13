using Bravo.Shared.Entities;

namespace Bravo.Domain.Auth.Entities;

public class Permission : Entity
{
    /// <summary>
    /// Example: 
    /// </summary>
    public string Resource { get; private set; }
    /// <summary>
    /// Example: Update, create, delete
    /// Todo: it can be an enum, const or even bits
    /// </summary>
    public string Action { get; private set; }
    /// <summary>
    /// Example: Products
    /// </summary>
    public string Module { get; private set; }
}
