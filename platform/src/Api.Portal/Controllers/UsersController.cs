using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/users")]
[Authorize]
public class UsersController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetUsers()
    {
        var users = await db.Users
            .Include(u => u.Role)
            .Where(u => u.TenantId == tenantContext.TenantId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserResponse(u.Id, u.Email, u.Role.Slug, u.IsActive, u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("invite")]
    public async Task<ActionResult<UserResponse>> InviteUser([FromBody] InviteUserRequest request)
    {
        var memberRole = await db.Roles.FirstOrDefaultAsync(r => r.Slug == "member");
        if (memberRole is null) return StatusCode(500, new { error = "Role 'member' not found." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId!.Value,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RoleId = memberRole.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new UserResponse(user.Id, user.Email, memberRole.Slug, user.IsActive, user.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantContext.TenantId);

        if (user is null) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
