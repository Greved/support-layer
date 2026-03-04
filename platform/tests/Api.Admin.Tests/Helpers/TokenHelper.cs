using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Api.Admin.Tests.Helpers;

public static class TokenHelper
{
    // Must match appsettings.json AdminJwt:Key
    private const string AdminKey = "CHANGE_ME_ADMIN_32_CHARS_MIN_PLACEHOLDER";
    private const string AdminIssuer = "supportlayer-admin";
    private const string AdminAudience = "supportlayer-admin";

    public static string AdminToken(Guid adminId, string email = "admin@test.com")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", adminId.ToString()),
            new Claim("email", email),
            new Claim("role", "superadmin"),
        };

        var token = new JwtSecurityToken(
            issuer: AdminIssuer,
            audience: AdminAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
