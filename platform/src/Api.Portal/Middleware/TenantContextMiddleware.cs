using Core.Auth;

namespace Api.Portal.Middleware;

public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst("sub")?.Value
                   ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            var role = context.User.FindFirst("role")?.Value;

            if (Guid.TryParse(sub, out var userId))
                tenantContext.UserId = userId;

            if (Guid.TryParse(tenantId, out var tid))
                tenantContext.TenantId = tid;

            tenantContext.Role = role;
        }

        await next(context);
    }
}
