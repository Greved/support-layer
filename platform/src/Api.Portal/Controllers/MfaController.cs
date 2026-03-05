using System.Security.Claims;
using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Api.Portal.Services;
using Core.Auth;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/auth/mfa")]
public class MfaController(AppDbContext db, IMfaService mfaService, ITokenService tokenService, TenantContext tenantContext) : ControllerBase
{
    [Authorize]
    [HttpPost("enroll")]
    public async Task<ActionResult<MfaEnrollResponse>> Enroll()
    {
        if (tenantContext.UserId is null) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == tenantContext.UserId.Value);
        if (user is null) return NotFound();

        var secret = mfaService.GenerateSecret();
        user.TotpSecret = secret;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var totpUri = mfaService.GetTotpUri(secret, user.Email);
        var backupCodes = mfaService.GenerateBackupCodes();

        return Ok(new MfaEnrollResponse(totpUri, secret, backupCodes));
    }

    [Authorize]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] MfaVerifyRequest request)
    {
        if (tenantContext.UserId is null) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == tenantContext.UserId.Value);
        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new { error = "MFA enrollment not started." });

        if (!mfaService.VerifyTotp(user.TotpSecret, request.Code))
            return BadRequest(new { error = "Invalid TOTP code." });

        user.MfaEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "MFA enabled successfully." });
    }

    /// <summary>
    /// Step 2 of MFA login — supply the TOTP code after a normal login that returned mfaRequired:true.
    /// Uses a short-lived temp token issued during step 1.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> MfaLogin([FromBody] MfaLoginRequest request)
    {
        var principal = tokenService.ValidateTempToken(request.TempToken);
        if (principal is null)
            return Unauthorized(new { error = "Invalid or expired temporary token." });

        // Temp token uses "sub" directly (not mapped by JWT middleware)
        var subValue = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subValue, out var userId))
            return Unauthorized(new { error = "Invalid token." });

        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "User not found." });

        if (string.IsNullOrEmpty(user.TotpSecret) || !mfaService.VerifyTotp(user.TotpSecret, request.Code))
            return BadRequest(new { error = "Invalid TOTP code." });

        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = await tokenService.IssueRefreshTokenAsync(user.Id);
        return Ok(new TokenResponse(accessToken, refreshToken));
    }
}
