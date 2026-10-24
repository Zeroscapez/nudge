using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Nudge.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    // The logged-in user's id, read from the "sub" claim of their token.
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedException("Not logged in.");
}
