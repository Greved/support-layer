using System.Security.Cryptography;
using Api.Portal.Models.Requests;
using Api.Portal.Services;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/auth/password-reset")]
public class PasswordResetController(AppDbContext db, IEmailService emailService, IConfiguration config) : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> RequestReset([FromBody] PasswordResetRequestRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        // Always return 200 to prevent email enumeration
        if (user is null)
            return Ok(new { message = "If the email is registered, a reset link has been sent." });

        // Invalidate old tokens for this user
        var old = db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.UsedAt == null);
        db.PasswordResetTokens.RemoveRange(old);

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var baseUrl = config["Portal:BaseUrl"] ?? "http://localhost:5173";
        var resetLink = $"{baseUrl}/reset-password?token={rawToken}";
        await emailService.SendPasswordResetAsync(user.Email, resetLink);

        return Ok(new { message = "If the email is registered, a reset link has been sent." });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] PasswordResetConfirmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Token)));

        var record = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (record is null || record.UsedAt.HasValue)
            return BadRequest(new { error = "Invalid or already used token." });

        if (record.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { error = "Token has expired." });

        record.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        record.User.UpdatedAt = DateTime.UtcNow;
        record.UsedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { message = "Password has been reset successfully." });
    }
}
