namespace Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TotpSecret { get; set; }
    public bool MfaEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
