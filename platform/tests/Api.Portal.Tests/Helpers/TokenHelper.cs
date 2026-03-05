using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Api.Portal.Tests.Helpers;

public static class TokenHelper
{
    // Must match Api.Portal appsettings.json Jwt:Key
    private const string Key = "CHANGE_ME_32_CHARS_MIN_PLACEHOLDER";
    private const string Issuer = "supportlayer";
    private const string Audience = "supportlayer";

    public static string PortalToken(Guid userId, Guid tenantId, string role = "owner", string email = "owner@test.com")
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("email", email),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("role", role),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void SetPortalToken(this HttpClient client, Guid userId, Guid tenantId, string role = "owner")
        => client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", PortalToken(userId, tenantId, role));
}
