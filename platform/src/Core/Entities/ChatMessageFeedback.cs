namespace Core.Entities;

public class ChatMessageFeedback
{
    public Guid Id { get; set; }
    public Guid ChatMessageId { get; set; }
    public Guid TenantId { get; set; }
    public string Rating { get; set; } = string.Empty; // up | down
    public string? Comment { get; set; }
    public bool Flagged { get; set; }
    public bool Promoted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PromotedAt { get; set; }

    public ChatMessage ChatMessage { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
