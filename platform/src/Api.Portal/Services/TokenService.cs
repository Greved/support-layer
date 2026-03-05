using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.Data;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TokenValidationParameters = Microsoft.IdentityModel.Tokens.TokenValidationParameters;

namespace Api.Portal.Services;

public class TokenService(IConfiguration configuration, AppDbContext db) : ITokenService
{
    public string IssueTempToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim("sub", user.Id.ToString()), new Claim("mfa_pending", "true") };
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateTempToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = key,
            }, out _);
            return principal.FindFirstValue("mfa_pending") == "true" ? principal : null;
        }
        catch { return null; }
    }

    public string IssueAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("role", user.Role?.Slug ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> IssueRefreshTokenAsync(Guid userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToHexString(bytes);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<(string accessToken, string refreshToken)> RefreshAsync(string token)
    {
        var row = await db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(r => r.Token == token);

        if (row is null || row.IsRevoked || row.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        row.IsRevoked = true;
        await db.SaveChangesAsync();

        var newAccess = IssueAccessToken(row.User);
        var newRefresh = await IssueRefreshTokenAsync(row.UserId);
        return (newAccess, newRefresh);
    }

    public async Task RevokeAsync(string token)
    {
        var row = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
        if (row is not null && !row.IsRevoked)
        {
            row.IsRevoked = true;
            await db.SaveChangesAsync();
        }
    }
}
