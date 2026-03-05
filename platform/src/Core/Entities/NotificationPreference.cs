namespace Core.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
