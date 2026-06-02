using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.OpenFinance.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        // Reads the client id from the JWT subject. Checks both "sub" and the mapped
        // NameIdentifier claim so it works regardless of inbound-claim mapping.
        public static int? GetClientId(this ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out var id) ? id : null;
        }
    }
}
