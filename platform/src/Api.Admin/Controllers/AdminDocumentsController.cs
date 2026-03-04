using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/tenants/{tenantId:guid}/documents")]
[Authorize]
public class AdminDocumentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid tenantId)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound();

        var docs = await db.Documents
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new AdminDocumentResponse(
                d.Id, d.FileName, d.Status,
                d.SizeBytes, d.ChunkCount,
                d.CreatedAt, d.UpdatedAt))
            .ToListAsync();

        return Ok(docs);
    }

    [HttpDelete("{docId:guid}")]
    public async Task<IActionResult> Delete(Guid tenantId, Guid docId)
    {
        var doc = await db.Documents
            .FirstOrDefaultAsync(d => d.Id == docId && d.TenantId == tenantId);

        if (doc is null) return NotFound();

        doc.IsActive = false;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }
}
