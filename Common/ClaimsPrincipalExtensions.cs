using System.Security.Claims;

namespace Appifylab.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User id claim missing from token.");
        return Guid.Parse(sub);
    }
}