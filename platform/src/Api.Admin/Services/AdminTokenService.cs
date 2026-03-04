using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Api.Admin.Services;

public class AdminTokenService(IConfiguration configuration) : IAdminTokenService
{
    public string IssueAdminToken(AdminUser admin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["AdminJwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", admin.Id.ToString()),
            new Claim("email", admin.Email),
            new Claim("name", admin.Name),
            new Claim("role", "superadmin"),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["AdminJwt:Issuer"],
            audience: configuration["AdminJwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string IssueImpersonationToken(Tenant tenant, Guid tenantAdminUserId, string email, string roleSlug, Guid impersonatingAdminId)
    {
        // Issues a portal-compatible JWT so the admin can log into the portal as this tenant
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", tenantAdminUserId.ToString()),
            new Claim("email", email),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("role", roleSlug),
            new Claim("impersonated_by", impersonatingAdminId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
