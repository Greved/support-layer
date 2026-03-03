using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Api.Portal.Services;
using Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/auth")]
public class AuthController(AppDbContext db, ITokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = await tokenService.IssueRefreshTokenAsync(user.Id);

        return Ok(new TokenResponse(accessToken, refreshToken));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            var (access, refresh) = await tokenService.RefreshAsync(request.RefreshToken);
            return Ok(new TokenResponse(access, refresh));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await tokenService.RevokeAsync(request.RefreshToken);
        return NoContent();
    }
}
