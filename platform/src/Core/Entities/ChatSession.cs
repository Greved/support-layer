namespace Core.Entities;

public class ChatSession
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
