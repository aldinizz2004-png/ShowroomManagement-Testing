using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Showroom.Api.Auth;

public record CurrentUser(int UserId, string Role)
{
    public bool IsStaff => Role is Roles.Admin or Roles.SalesRep;

    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var role = principal.FindFirstValue("role");
        return new CurrentUser(int.TryParse(id, out var userId) ? userId : 0, role ?? string.Empty);
    }
}
