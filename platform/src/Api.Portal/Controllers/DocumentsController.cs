using Api.Portal.Constants;
using Api.Portal.Jobs;
using Api.Portal.Models.Responses;
using Api.Portal.Services;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/documents")]
[Authorize]
public class DocumentsController(
    AppDbContext db,
    TenantContext tenantContext,
    IStorageService storage,
    IAntivirusScanner antivirusScanner,
    IBackgroundJobClient backgroundJobs) : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
        "text/csv",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md", ".html", ".htm", ".csv",
    };

    [HttpGet]
    public async Task<ActionResult<List<DocumentResponse>>> GetDocuments()
    {
        var docs = await db.Documents
            .Where(d => d.TenantId == tenantContext.TenantId && d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentResponse(
                d.Id, d.FileName, d.Status, d.SizeBytes, d.ChunkCount,
                d.ContentType, d.ErrorMessage, d.CreatedAt))
            .ToListAsync();

        return Ok(docs);
    }

    [HttpPost]
    [RequestSizeLimit(512 * 1024 * 1024)] // 512 MB hard cap
    public async Task<ActionResult<DocumentResponse>> UploadDocument(IFormFile file)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var ext = Path.GetExtension(file.FileName);

        // 1. Extension allowlist
        if (!AllowedExtensions.Contains(ext))
            return StatusCode(415, new { error = $"File type '{ext}' is not supported." });

        // 1b. MIME-type allowlist
        if (!AllowedContentTypes.Contains(file.ContentType ?? ""))
            return StatusCode(415, new { error = "unsupported_file_type" });

        // 2. Load plan limits
        var tenant = await db.Tenants
            .Include(t => t.Plan).ThenInclude(p => p.Limit)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant?.Plan?.Limit is null)
            return StatusCode(500, new { error = "Plan limits not configured." });

        var limit = tenant.Plan.Limit;

        // 3. File size check
        var maxBytes = (long)limit.MaxFileSizeMb * 1024 * 1024;
        if (limit.MaxFileSizeMb > 0 && file.Length > maxBytes)
            return StatusCode(413, new { error = $"File exceeds the {limit.MaxFileSizeMb} MB limit." });

        // 4. Document count check
        if (limit.MaxDocuments > 0)
        {
            var activeCount = await db.Documents.CountAsync(
                d => d.TenantId == tenantId && d.IsActive && d.Status != DocumentStatus.Superseded);
            if (activeCount >= limit.MaxDocuments)
                return StatusCode(429, new { error = "Document limit reached for your plan." });
        }

        // 5. Antivirus scan (ClamAV)
        await using (var scanStream = file.OpenReadStream())
        {
            var scanResult = await antivirusScanner.ScanAsync(scanStream, HttpContext.RequestAborted);
            if (scanResult.Status == AntivirusScanStatus.Infected)
            {
                return UnprocessableEntity(new
                {
                    error = "virus_detected",
                    signature = scanResult.Signature,
                });
            }

            if (scanResult.Status == AntivirusScanStatus.Unavailable)
                return StatusCode(503, new { error = "antivirus_unavailable" });
        }

        // 6. Supersede existing document with same name
        var existing = await db.Documents.FirstOrDefaultAsync(
            d => d.TenantId == tenantId && d.FileName == file.FileName
              && d.IsActive && d.Status != DocumentStatus.Superseded);

        if (existing is not null)
        {
            existing.Status = DocumentStatus.Superseded;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        // 7. Save file
        var docId = Guid.NewGuid();
        string storagePath;
        await using (var stream = file.OpenReadStream())
        {
            storagePath = await storage.SaveAsync(tenantId, docId, file.FileName, stream);
        }

        // 8. Insert Document row
        var contentType = file.ContentType ?? "application/octet-stream";
        var document = new Document
        {
            Id = docId,
            TenantId = tenantId,
            FileName = file.FileName,
            StoragePath = storagePath,
            Status = DocumentStatus.Pending,
            SizeBytes = file.Length,
            ContentType = contentType,
            UploadedById = tenantContext.UserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        // 9. Enqueue ingestion
        backgroundJobs.Enqueue<IngestionJob>(j => j.RunAsync(docId));

        return Ok(new DocumentResponse(
            document.Id, document.FileName, document.Status, document.SizeBytes,
            document.ChunkCount, document.ContentType, document.ErrorMessage, document.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(
            d => d.Id == id && d.TenantId == tenantContext.TenantId);

        if (doc is null) return NotFound();
        if (doc.TenantId != tenantContext.TenantId) return Forbid();

        doc.IsActive = false;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<DocumentStatusResponse>> GetStatus(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(
            d => d.Id == id && d.TenantId == tenantContext.TenantId);

        if (doc is null) return NotFound();

        return Ok(new DocumentStatusResponse(doc.Id, doc.Status, doc.ErrorMessage, doc.ChunkCount));
    }
}
