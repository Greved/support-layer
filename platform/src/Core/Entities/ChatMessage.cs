namespace Core.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
}
