using Api.Admin.Models.Responses;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/feedback")]
[Authorize]
public class FeedbackController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FeedbackItemResponse>>> List(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] bool flaggedOnly = false,
        [FromQuery] bool includePromoted = true
    )
    {
        var query = db.ChatMessageFeedback
            .Include(f => f.ChatMessage)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(f => f.TenantId == tenantId.Value);
        if (flaggedOnly)
            query = query.Where(f => f.Flagged);
        if (!includePromoted)
            query = query.Where(f => !f.Promoted);

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(200)
            .Select(f => new
            {
                f.Id,
                f.ChatMessageId,
                f.TenantId,
                f.Rating,
                f.Comment,
                f.Flagged,
                f.Promoted,
                f.CreatedAt,
                MessageContent = f.ChatMessage.Content,
            })
            .ToListAsync();

        var result = rows.Select(f => new FeedbackItemResponse(
                f.Id,
                f.ChatMessageId,
                f.TenantId,
                f.Rating,
                f.Comment,
                f.Flagged,
                f.Promoted,
                f.CreatedAt,
                f.MessageContent.Length > 120
                    ? f.MessageContent[..120]
                    : f.MessageContent
            )).ToList();

        return Ok(result);
    }

    [HttpPost("{id:guid}/promote")]
    public async Task<ActionResult<FeedbackPromoteResponse>> Promote(Guid id)
    {
        var feedback = await db.ChatMessageFeedback
            .Include(f => f.ChatMessage)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (feedback is null) return NotFound();

        var existingDataset = await db.EvalDatasets
            .FirstOrDefaultAsync(d => d.SourceFeedbackId == feedback.Id);

        if (existingDataset is null)
        {
            var assistantMessage = feedback.ChatMessage;
            var question = await db.ChatMessages
                .Where(m =>
                    m.SessionId == assistantMessage.SessionId
                    && m.CreatedAt <= assistantMessage.CreatedAt
                    && m.Role == "user")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            question = string.IsNullOrWhiteSpace(question)
                ? assistantMessage.Content
                : question.Trim();

            var groundTruth = string.IsNullOrWhiteSpace(feedback.Comment)
                ? assistantMessage.Content
                : feedback.Comment.Trim();

            db.EvalDatasets.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = feedback.TenantId,
                SourceFeedbackId = feedback.Id,
                Question = Truncate(question, 4000),
                GroundTruth = Truncate(groundTruth, 8000),
                SourceChunkIdsJson = "[]",
                QuestionType = feedback.Rating == "down" ? "feedback_negative" : "feedback_positive",
                DatasetVersion = DateTime.UtcNow.ToString("yyyyMMdd"),
                CreatedAt = DateTime.UtcNow,
            });
        }

        feedback.Promoted = true;
        feedback.PromotedAt = DateTime.UtcNow;
        feedback.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new FeedbackPromoteResponse(feedback.Id, feedback.Promoted, feedback.PromotedAt));
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
