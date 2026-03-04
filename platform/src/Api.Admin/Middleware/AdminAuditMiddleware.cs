using System.Security.Claims;
using Core.Data;
using Core.Entities;

namespace Api.Admin.Middleware;

/// <summary>
/// Writes an AuditLog row for every mutating request (POST/PUT/PATCH/DELETE)
/// that reaches a /admin/tenants/* endpoint.
/// </summary>
public class AdminAuditMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        await next(context);

        if (!MutatingMethods.Contains(context.Request.Method))
            return;

        if (context.Response.StatusCode >= 400)
            return;

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/admin/tenants", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/admin/users", StringComparison.OrdinalIgnoreCase))
            return;

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        // Determine tenant ID from path segment /admin/tenants/{id}/...
        Guid? tenantId = null;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && segments[0] == "admin" && segments[1] == "tenants"
            && Guid.TryParse(segments[2], out var tid))
            tenantId = tid;

        if (tenantId is null)
            return;

        var adminId = context.User.FindFirst("sub")?.Value;
        Guid.TryParse(adminId, out var adminGuid);

        var action = $"{context.Request.Method}:{path}";
        var ip = context.Connection.RemoteIpAddress?.ToString();

        // Find the system/admin user tied to this tenant for actor tracking
        // Admin users don't have tenant affiliation — use the system tenant's first admin user
        // as a proxy. For now write UserId = null (superadmin action).
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = null,
            Action = action,
            ResourceType = "admin",
            ResourceId = adminGuid == Guid.Empty ? null : adminGuid.ToString(),
            Metadata = $"{{\"method\":\"{context.Request.Method}\",\"path\":\"{path}\"}}",
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow,
        });

        try { await db.SaveChangesAsync(); }
        catch { /* audit failure must not break the response */ }
    }
}
