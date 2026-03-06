using Api.Portal.Constants;
using Api.Portal.Services;
using Core.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Jobs;

public class IngestionJob(
    AppDbContext db,
    IStorageService storage,
    IRagClient ragClient,
    IBackgroundJobClient backgroundJobs,
    ILogger<IngestionJob> logger)
{
    public async Task RunAsync(Guid documentId)
    {
        var document = await db.Documents
            .Include(d => d.Tenant)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document is null)
        {
            logger.LogWarning("IngestionJob: document {DocumentId} not found", documentId);
            return;
        }

        document.Status = DocumentStatus.Processing;
        document.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        try
        {
            var fileBytes = await storage.ReadAllBytesAsync(document.StoragePath);
            var result = await ragClient.IngestAsync(
                document.Tenant.Slug,
                document.Id.ToString(),
                document.FileName,
                fileBytes,
                document.ContentType);

            document.Status = DocumentStatus.Ready;
            document.ChunkCount = result.ChunksWritten;
            backgroundJobs.Enqueue<IngestEvalTriggerJob>(j => j.RunAsync(document.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IngestionJob failed for document {DocumentId}", documentId);
            document.Status = DocumentStatus.Error;
            document.ErrorMessage = ex.Message;
        }
        finally
        {
            document.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
