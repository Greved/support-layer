using System.Security.Cryptography;
using System.Text;
using Api.Public.Services;
using Core.Auth;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Public.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db, TenantContext tenantContext, IRateLimiter rateLimiter)
    {
        // Extract key from X-Api-Key header or Authorization: Bearer sl_live_*
        string? plainKey = null;

        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            plainKey = apiKeyHeader.FirstOrDefault();
        }
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var bearer = authHeader.FirstOrDefault();
            if (bearer is not null && bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                plainKey = bearer["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrEmpty(plainKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key required" });
            return;
        }

        var keyHash = ComputeSha256Hex(plainKey);

        var apiKey = await db.ApiKeys
            .Include(k => k.Tenant)
            .ThenInclude(t => t.Plan)
            .ThenInclude(p => p.Limit)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (apiKey is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive API key" });
            return;
        }

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key has expired" });
            return;
        }

        var maxPerMinute = apiKey.Tenant.Plan.Limit?.MaxRequestsPerMinute ?? 10;

        try
        {
            await rateLimiter.CheckAsync(apiKey.Id, maxPerMinute, context.RequestAborted);
        }
        catch (RateLimitExceededException ex)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = ex.RetryAfterSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded" });
            return;
        }
        catch (RedisConnectionException ex)
        {
            // Phase 5 chaos requirement: Redis outage must not surface as a 500 with stack trace.
            logger.LogWarning(ex, "Redis unavailable in rate limiter; allowing request to proceed");
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout in rate limiter; allowing request to proceed");
        }

        tenantContext.TenantId = apiKey.TenantId;
        context.Items["ApiKeyId"] = apiKey.Id;
        context.Items["TenantSlug"] = apiKey.Tenant.Slug;

        await next(context);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
