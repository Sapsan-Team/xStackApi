using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace xstackApi.Endpoints;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(id, out var userId) ? userId : null;
    }
}
