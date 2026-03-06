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
[Route("portal/chat")]
[Authorize]
public class ChatController(AppDbContext db, TenantContext tenantContext, IRagClient ragClient) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return UnprocessableEntity(new { error = "query_required" });

        if (request.Query.Length > 10_000)
            return BadRequest(new { error = "query_too_long" });

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);
        if (tenant is null) return Unauthorized();

        var result = await ragClient.QueryAsync(tenant.Slug, request.Query);

        var sources = result.Sources.Select(s => new ChatSource(
            s.File, s.Page, s.Offset, s.RelevanceScore, s.BriefContent)).ToList();

        return Ok(new ChatResponse(result.Answer, sources));
    }
}
