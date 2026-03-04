using System.Text;
using System.Text.Json;
using Api.Public.Models.Requests;
using Api.Public.Models.Responses;
using Api.Public.Services;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Public.Controllers;

[ApiController]
[Route("v1")]
public class ChatController(AppDbContext db, TenantContext tenantContext, IPublicRagClient ragClient) : ControllerBase
{
    // POST /v1/chat — non-streaming
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);
        if (tenant is null) return Unauthorized();

        var apiKeyId = HttpContext.Items["ApiKeyId"] as Guid?;
        var session = await ResolveOrCreateSessionAsync(request.SessionId, tenant.Id, apiKeyId);

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "user",
            Content = request.Query,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await ragClient.QueryAsync(tenant.Slug, request.Query, HttpContext.RequestAborted);

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "assistant",
            Content = result.Answer,
            TokensUsed = 0,
            CreatedAt = DateTime.UtcNow,
        });

        db.BillingEvents.Add(new BillingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            EventType = "query",
            Amount = 0,
            ExternalRef = session.Id.ToString(),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        var sources = result.Sources.Select(s => new ChatSource(s.File, s.Page, s.Offset, s.RelevanceScore, s.BriefContent)).ToList();
        return Ok(new ChatResponse(result.Answer, session.Id, sources));
    }

    // POST /v1/chat/stream — SSE streaming
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] ChatRequest request)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);
        if (tenant is null)
        {
            Response.StatusCode = 401;
            return;
        }

        var apiKeyId = HttpContext.Items["ApiKeyId"] as Guid?;
        var session = await ResolveOrCreateSessionAsync(request.SessionId, tenant.Id, apiKeyId);

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "user",
            Content = request.Query,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var answerBuilder = new StringBuilder();

        await foreach (var line in ragClient.StreamQueryAsync(tenant.Slug, request.Query, HttpContext.RequestAborted))
        {
            var data = line.StartsWith("data:") ? line["data:".Length..].Trim() : line;

            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                if (type == "token" && root.TryGetProperty("chunk", out var chunk))
                    answerBuilder.Append(chunk.GetString());

                if (type == "done")
                {
                    var finalAnswer = root.TryGetProperty("answer", out var ans) ? ans.GetString() ?? answerBuilder.ToString() : answerBuilder.ToString();

                    db.ChatMessages.Add(new ChatMessage
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Role = "assistant",
                        Content = finalAnswer,
                        TokensUsed = 0,
                        CreatedAt = DateTime.UtcNow,
                    });
                    db.BillingEvents.Add(new BillingEvent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        EventType = "query",
                        Amount = 0,
                        ExternalRef = session.Id.ToString(),
                        CreatedAt = DateTime.UtcNow,
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (JsonException) { /* pass non-JSON lines through */ }

            var sseBytes = Encoding.UTF8.GetBytes($"{line}\n\n");
            await Response.Body.WriteAsync(sseBytes, HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        var doneBytes = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
        await Response.Body.WriteAsync(doneBytes, HttpContext.RequestAborted);
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }

    // POST /v1/session — create session
    [HttpPost("session")]
    public async Task<ActionResult> CreateSession()
    {
        if (tenantContext.TenantId is null) return Unauthorized();

        var apiKeyId = HttpContext.Items["ApiKeyId"] as Guid?;
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId.Value,
            ApiKeyId = apiKeyId,
            Title = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();

        return Ok(new { id = session.Id, createdAt = session.CreatedAt });
    }

    // GET /v1/session/{id}
    [HttpGet("session/{id:guid}")]
    public async Task<ActionResult> GetSession(Guid id)
    {
        var session = await db.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null) return NotFound();
        if (session.TenantId != tenantContext.TenantId) return Forbid();

        var messages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { id = m.Id, role = m.Role, content = m.Content, createdAt = m.CreatedAt })
            .ToList();

        return Ok(new { id = session.Id, createdAt = session.CreatedAt, messages });
    }

    private async Task<ChatSession> ResolveOrCreateSessionAsync(Guid? sessionId, Guid tenantId, Guid? apiKeyId)
    {
        if (sessionId.HasValue)
        {
            var existing = await db.ChatSessions.FirstOrDefaultAsync(
                s => s.Id == sessionId.Value && s.TenantId == tenantId);
            if (existing is not null) return existing;
        }

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApiKeyId = apiKeyId,
            Title = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }
}
