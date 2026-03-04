using Api.Admin.Models.Requests;
using Api.Admin.Models.Responses;
using Api.Admin.Services;
using Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/auth")]
public class AuthController(AppDbContext db, IAdminTokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.Email == request.Email && a.IsActive);

        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        var token = tokenService.IssueAdminToken(admin);
        return Ok(new AdminLoginResponse(token, admin.Id.ToString(), admin.Email, admin.Name));
    }
}
